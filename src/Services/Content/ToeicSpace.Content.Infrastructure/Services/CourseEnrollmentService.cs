using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using ToeicSpace.Content.Application.Common.Pagination;
using ToeicSpace.Content.Application.DTOs.Courses;
using ToeicSpace.Content.Application.Interfaces.Services;
using ToeicSpace.Content.Domain.Entities;
using ToeicSpace.Content.Domain.Enums;
using ToeicSpace.Content.Domain.Exceptions;
using ToeicSpace.Content.Infrastructure.Persistence;

namespace ToeicSpace.Content.Infrastructure.Services;

public sealed class CourseEnrollmentService(ContentDbContext db, ILessonMediaStore media,
    IConfiguration config, TimeProvider clock) : ICourseEnrollmentService
{
    private DateTime Now => clock.GetUtcNow().UtcDateTime;
    private int ReservationMinutes => config.GetValue("Checkout:ReservationMinutes", 15) is var minutes and > 0 and <= 60
        ? minutes : throw new InvalidOperationException("Checkout reservation must be between 1 and 60 minutes.");

    public async Task<CheckoutCourseDto> GetCheckoutAsync(Guid courseId, Guid userId, CancellationToken ct)
    {
        var course = await db.Courses.AsNoTracking().SingleOrDefaultAsync(c => c.Id == courseId, ct);
        if (course?.Status != CourseStatus.Published) throw ContentRuleException.NotFound();
        var owned = await db.CourseEnrollments.AnyAsync(e => e.CourseId == courseId && e.UserId == userId, ct);
        return new CheckoutCourseDto(courseId, course.Price, "VND", null, null, owned);
    }

    public Task<CheckoutCourseDto> ReserveAsync(Guid courseId, Guid userId, Guid reference, CancellationToken ct) =>
        WithCourseLock(courseId, async course =>
        {
            if (reference == Guid.Empty || userId == Guid.Empty || course.Status != CourseStatus.Published || course.Price <= 0)
                throw new ContentRuleException("checkout_unavailable", "Paid published course required.", 409);
            if (await db.CourseEnrollments.AnyAsync(e => e.CourseId == courseId && e.UserId == userId, ct))
                throw new ContentRuleException("already_enrolled", "Course already owned.", 409);
            var existing = await db.CourseSlotReservations.SingleOrDefaultAsync(r => r.CheckoutReference == reference, ct);
            if (existing is not null)
            {
                if (existing.UserId != userId || existing.CourseId != courseId || existing.Status != SlotReservationStatus.Reserved || existing.ExpiresAt <= Now)
                    throw new ContentRuleException("invalid_reservation", "Checkout reference is unavailable.", 409);
                return new CheckoutCourseDto(courseId, course.Price, "VND", existing.ExpiresAt, existing.Id, false);
            }
            if (course.Capacity is null) return new CheckoutCourseDto(courseId, course.Price, "VND", null, null, false);
            if (await db.CourseSlotReservations.AnyAsync(r => r.UserId == userId && r.CourseId == courseId && r.Status == SlotReservationStatus.Reserved, ct))
                throw new ContentRuleException("pending_checkout", "There is already an active checkout for this course.", 409);
            if (await OccupiedAsync(courseId, ct) >= course.Capacity.Value)
                throw new ContentRuleException("course_full", "No course slots are available.", 409);
            var reservation = new CourseSlotReservation
            {
                CheckoutReference = reference, UserId = userId, CourseId = courseId,
                ExpiresAt = Now.AddMinutes(ReservationMinutes)
            };
            db.CourseSlotReservations.Add(reservation);
            return new CheckoutCourseDto(courseId, course.Price, "VND", reservation.ExpiresAt, reservation.Id, false);
        }, ct);

    public async Task ReleaseAsync(Guid reference, CancellationToken ct)
    {
        var courseId = await db.CourseSlotReservations.AsNoTracking().Where(r => r.CheckoutReference == reference)
            .Select(r => r.CourseId).SingleOrDefaultAsync(ct);
        if (courseId == Guid.Empty) return;
        await WithCourseLock(courseId, async _ =>
        {
            var reservation = await db.CourseSlotReservations.SingleAsync(r => r.CheckoutReference == reference, ct);
            if (reservation.Status == SlotReservationStatus.Reserved) reservation.Status = SlotReservationStatus.Released;
            return true;
        }, ct);
    }

    public Task<CourseEnrollment> FreeEnrollAsync(Guid courseId, Guid userId, CancellationToken ct) =>
        WithCourseLock(courseId, async course =>
        {
            if (userId == Guid.Empty)
                throw new ContentRuleException("invalid_subject", "Invalid user identity.", 401);
            var existing = await db.CourseEnrollments.SingleOrDefaultAsync(e => e.CourseId == courseId && e.UserId == userId, ct);
            if (existing is not null) return existing;
            if (course.Status != CourseStatus.Published || course.Price != 0)
                throw new ContentRuleException("free_enrollment_unavailable", "Course is not a published free course.", 409);
            var held = await db.CourseSlotReservations.SingleOrDefaultAsync(r => r.CourseId == courseId &&
                r.UserId == userId && r.Status == SlotReservationStatus.Reserved, ct);
            if (held is not null)
            {
                held.Status = SlotReservationStatus.Confirmed;
                held.ConfirmedAt = Now;
            }
            else if (course.Capacity is { } capacity && await OccupiedAsync(courseId, ct) >= capacity)
                throw new ContentRuleException("course_full", "No course slots are available.", 409);
            var enrollment = new CourseEnrollment { CourseId = courseId, UserId = userId, EnrolledAt = Now };
            db.CourseEnrollments.Add(enrollment);
            return enrollment;
        }, ct);

    public Task<CourseEnrollment> ConfirmAsync(Guid courseId, Guid userId, Guid reference, DateTime paidAt,
        bool requiresReservation, CancellationToken ct) =>
        WithCourseLock(courseId, async course =>
        {
            var existing = await db.CourseEnrollments.SingleOrDefaultAsync(e => e.CourseId == courseId && e.UserId == userId, ct);
            if (existing is not null) return existing;
            var reservation = await db.CourseSlotReservations.SingleOrDefaultAsync(r => r.CheckoutReference == reference, ct);
            if (reservation is not null)
            {
                if (reservation.UserId != userId || reservation.CourseId != courseId ||
                    reservation.Status != SlotReservationStatus.Reserved || paidAt > reservation.ExpiresAt)
                    throw new ContentRuleException("reservation_not_fulfillable", "Course slot was released or payment was late.", 409);
                reservation.Status = SlotReservationStatus.Confirmed;
                reservation.ConfirmedAt = Now;
            }
            else if (requiresReservation || course.Capacity is { } capacity && await OccupiedAsync(courseId, ct) >= capacity)
                throw new ContentRuleException("reservation_not_fulfillable", "Course slot was released or is unavailable.", 409);
            var enrollment = new CourseEnrollment { CourseId = courseId, UserId = userId, EnrolledAt = Now };
            db.CourseEnrollments.Add(enrollment);
            return enrollment;
        }, ct);

    public Task<CourseEnrollment?> StatusAsync(Guid courseId, Guid userId, CancellationToken ct) =>
        db.CourseEnrollments.AsNoTracking().SingleOrDefaultAsync(e => e.CourseId == courseId && e.UserId == userId, ct);

    public async Task<PagedResult<MyCourseDto>> MineAsync(Guid userId, string? search, int page, int pageSize, CancellationToken ct)
    {
        if (page < 1 || pageSize is < 1 or > 100 || page > int.MaxValue / pageSize)
            throw new ContentRuleException("invalid_pagination", "Invalid page or page size.");
        var query = db.CourseEnrollments.AsNoTracking().Join(db.Courses, e => e.CourseId, c => c.Id, (e, c) => new { e, c })
            .Where(x => x.e.UserId == userId);
        if (!string.IsNullOrWhiteSpace(search)) query = query.Where(x => x.c.Title.Contains(search.Trim()));
        var total = await query.CountAsync(ct);
        var items = await query.OrderByDescending(x => x.e.EnrolledAt).ThenBy(x => x.e.CourseId)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(x => new { x.c.Id, x.c.Title, x.c.Slug, x.c.ShortDescription, x.c.ThumbnailKey, x.e.Status, x.e.EnrolledAt })
            .ToListAsync(ct);
        var result = new List<MyCourseDto>(items.Count);
        foreach (var item in items)
            result.Add(new MyCourseDto(item.Id, item.Title, item.Slug, item.ShortDescription, item.Status,
                item.EnrolledAt, item.ThumbnailKey is null ? null :
                    await media.PresignReadAsync(item.ThumbnailKey, TimeSpan.FromMinutes(5), ct)));
        return new PagedResult<MyCourseDto>(result, page, pageSize, total);
    }

    public async Task<PublicLessonPreviewDto> LessonAsync(Guid courseId, Guid lessonId, Guid userId, CancellationToken ct)
    {
        if (!await db.CourseEnrollments.AnyAsync(e => e.CourseId == courseId && e.UserId == userId &&
            e.Status == EnrollmentStatus.Active, ct)) throw ContentRuleException.NotFound();
        var lesson = await db.Lessons.AsNoTracking().Include(x => x.Videos).Include(x => x.Resources).AsSplitQuery()
            .SingleOrDefaultAsync(x => x.Id == lessonId && x.Status == LessonStatus.Published &&
                x.Module.CourseId == courseId && x.Module.Course.Status == CourseStatus.Published, ct)
            ?? throw ContentRuleException.NotFound();
        var videos = new List<PublicVideoDto>();
        foreach (var video in lesson.Videos.OrderBy(x => x.SortOrder))
            videos.Add(new PublicVideoDto(video.Id, video.FileName, video.ContentType, video.FileSize,
                video.DurationSeconds, video.SortOrder, await media.PresignReadAsync(video.ObjectKey, TimeSpan.FromMinutes(5), ct)));
        var resources = new List<PublicAttachmentDto>();
        foreach (var file in lesson.Resources.OrderBy(x => x.CreatedAt))
            resources.Add(new PublicAttachmentDto(file.Id, file.FileName, file.ContentType, file.FileSize,
                await media.PresignReadAsync(file.ObjectKey, TimeSpan.FromMinutes(5), ct)));
        return new PublicLessonPreviewDto(lesson.Id, lesson.Title, lesson.TextContent, videos, resources);
    }

    private async Task<int> OccupiedAsync(Guid courseId, CancellationToken ct) =>
        await db.CourseEnrollments.CountAsync(e => e.CourseId == courseId, ct) +
        await db.CourseSlotReservations.CountAsync(r => r.CourseId == courseId && r.Status == SlotReservationStatus.Reserved, ct);

    private async Task<T> WithCourseLock<T>(Guid courseId, Func<Course, Task<T>> work, CancellationToken ct)
    {
        try
        {
            await using var transaction = await db.Database.BeginTransactionAsync(ct);
            var course = await db.Courses.FromSqlInterpolated($"SELECT * FROM Courses WHERE Id = {courseId} FOR UPDATE")
                .SingleOrDefaultAsync(ct) ?? throw ContentRuleException.NotFound();
            var result = await work(course);
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            return result;
        }
        catch (Microsoft.EntityFrameworkCore.DbUpdateException ex) when (ex.InnerException is MySqlConnector.MySqlException { Number: 1062 })
        { throw new ContentRuleException("duplicate_enrollment", "This course is already enrolled or reserved.", 409, ex); }
        catch (Microsoft.EntityFrameworkCore.DbUpdateException ex) when (ex.InnerException is MySqlConnector.MySqlException { Number: 1205 or 1213 })
        { throw new ContentRuleException("concurrency_conflict", "Concurrent checkout; retry.", 409, ex); }
        catch (MySqlConnector.MySqlException ex) when (ex.Number is 1205 or 1213)
        { throw new ContentRuleException("concurrency_conflict", "Concurrent checkout; retry.", 409, ex); }
    }
}
