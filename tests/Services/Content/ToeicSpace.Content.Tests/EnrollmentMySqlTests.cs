using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using ToeicSpace.Content.Application.Interfaces.Services;
using ToeicSpace.Content.Application.Features.Courses.Commands.UpdateCoursePricing;
using ToeicSpace.Content.Domain.Entities;
using ToeicSpace.Content.Domain.Enums;
using ToeicSpace.Content.Domain.Exceptions;
using ToeicSpace.Content.Infrastructure.Persistence;
using ToeicSpace.Content.Infrastructure.Services;
using ToeicSpace.Content.Infrastructure.Persistence.Repositories;
using Xunit;

namespace ToeicSpace.Content.Tests;

public sealed class EnrollmentMySqlTests
{
    private static string? Connection => Environment.GetEnvironmentVariable("PBL57_CONTENT_TEST_DB");
    private static ContentDbContext Db() => new(new DbContextOptionsBuilder<ContentDbContext>()
        .UseMySql(Connection!, new MySqlServerVersion(new Version(8, 4, 0))).Options);
    private static CourseEnrollmentService Service(ContentDbContext db, TimeProvider? clock = null) => new(db, new NoMedia(),
        new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        { ["Checkout:ReservationMinutes"] = "15" }).Build(), clock ?? TimeProvider.System);

    private static async Task<Course> CourseAsync(decimal price, int? capacity)
    {
        await using var db = Db();
        var id = Guid.NewGuid();
        var course = new Course
        {
            Id = id, Code = id.ToString("N"), Slug = "course-" + id.ToString("N"), Title = "Test Course",
            Status = CourseStatus.Published, Price = price, Capacity = capacity,
            ExamType = CourseExamType.ListeningReading, Level = CourseLevel.Beginner,
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow,
            Skills = ["Listening"], Objectives = ["Learn"], Description = "Description", ShortDescription = "Short"
        };
        db.Courses.Add(course);
        await db.SaveChangesAsync();
        return course;
    }

    [Fact]
    public async Task RemainingSeats_CountsReservationsAndEnrollments_AndMyCoursesSignsThumbnail()
    {
        if (Connection is null) return;
        var course = await CourseAsync(100000, 2);
        var user = Guid.NewGuid();
        var reference = Guid.NewGuid();
        await using var db = Db();
        var repository = new PublicCourseRepository(db);
        Assert.Equal(2, (await repository.GetRemainingSeatsAsync([course.Id], default))[course.Id]);
        await Service(db).ReserveAsync(course.Id, user, reference, default);
        Assert.Equal(1, (await repository.GetRemainingSeatsAsync([course.Id], default))[course.Id]);
        await Service(db).ConfirmAsync(course.Id, user, reference, DateTime.UtcNow, true, default);
        Assert.Equal(1, (await repository.GetRemainingSeatsAsync([course.Id], default))[course.Id]);
        var other = Guid.NewGuid();
        var otherReference = Guid.NewGuid();
        await Service(db).ReserveAsync(course.Id, other, otherReference, default);
        Assert.Equal(0, (await repository.GetRemainingSeatsAsync([course.Id], default))[course.Id]);
        await Service(db).ReleaseAsync(otherReference, default);
        Assert.Equal(1, (await repository.GetRemainingSeatsAsync([course.Id], default))[course.Id]);
        var thumbnailKey = "private/" + Guid.NewGuid().ToString("N");
        await db.Courses.Where(x => x.Id == course.Id).ExecuteUpdateAsync(x =>
            x.SetProperty(c => c.ThumbnailKey, thumbnailKey));
        var mine = await Service(db).MineAsync(user, null, 1, 20, default);
        Assert.Equal("https://signed.example.test/media?sig=short-lived", Assert.Single(mine.Items).ThumbnailUrl);
    }

    [Fact]
    public async Task LastSlot_OnlyOneConcurrentCheckoutSucceeds()
    {
        if (Connection is null) return;
        var course = await CourseAsync(100000, 1);
        async Task<bool> Reserve(Guid user)
        {
            await using var db = Db();
            try { await Service(db).ReserveAsync(course.Id, user, Guid.NewGuid(), default); return true; }
            catch (ContentRuleException ex) when (ex.Code is "course_full" or "concurrency_conflict") { return false; }
        }
        var result = await Task.WhenAll(Reserve(Guid.NewGuid()), Reserve(Guid.NewGuid()));
        Assert.Single(result, x => x);
        await using var verify = Db();
        Assert.Equal(1, await verify.CourseSlotReservations.CountAsync(x => x.CourseId == course.Id));
    }

    [Fact]
    public async Task LastFreeSlot_OnlyOneConcurrentUserCanEnroll()
    {
        if (Connection is null) return;
        var course = await CourseAsync(0, 1);
        async Task<bool> Enroll(Guid user)
        {
            await using var db = Db();
            try { await Service(db).FreeEnrollAsync(course.Id, user, default); return true; }
            catch (ContentRuleException ex) when (ex.Code is "course_full" or "concurrency_conflict") { return false; }
        }
        var result = await Task.WhenAll(Enroll(Guid.NewGuid()), Enroll(Guid.NewGuid()));
        Assert.Single(result, x => x);
        await using var verify = Db();
        Assert.Equal(1, await verify.CourseEnrollments.CountAsync(x => x.CourseId == course.Id));
    }

    [Fact]
    public async Task FreeEnrollment_DeduplicatesAndCannotBypassPaidCourse()
    {
        if (Connection is null) return;
        var free = await CourseAsync(0, 1);
        var paid = await CourseAsync(100000, 1);
        var user = Guid.NewGuid();
        await using var db = Db();
        var first = await Service(db).FreeEnrollAsync(free.Id, user, default);
        var second = await Service(db).FreeEnrollAsync(free.Id, user, default);
        Assert.Equal(first.Id, second.Id);
        Assert.Equal("free_enrollment_unavailable", (await Assert.ThrowsAsync<ContentRuleException>(() =>
            Service(db).FreeEnrollAsync(paid.Id, user, default))).Code);
        Assert.Equal("course_full", (await Assert.ThrowsAsync<ContentRuleException>(() =>
            Service(db).FreeEnrollAsync(free.Id, Guid.NewGuid(), default))).Code);
        await db.Courses.Where(x => x.Id == free.Id).ExecuteUpdateAsync(s => s.SetProperty(x => x.Price, 250000m));
        await using var fresh = Db();
        Assert.Equal(first.Id, (await Service(fresh).FreeEnrollAsync(free.Id, user, default)).Id);
        Assert.Equal("free_enrollment_unavailable", (await Assert.ThrowsAsync<ContentRuleException>(() =>
            Service(fresh).FreeEnrollAsync(free.Id, Guid.NewGuid(), default))).Code);
    }

    [Fact]
    public async Task ReleasedSlot_CanBeReusedAndConfirmationIsIdempotent()
    {
        if (Connection is null) return;
        var course = await CourseAsync(100000, 1);
        var firstRef = Guid.NewGuid();
        await using (var db = Db())
        {
            await Service(db).ReserveAsync(course.Id, Guid.NewGuid(), firstRef, default);
            await Service(db).ReleaseAsync(firstRef, default);
        }
        var secondUser = Guid.NewGuid();
        var secondRef = Guid.NewGuid();
        await using var second = Db();
        var slot = await Service(second).ReserveAsync(course.Id, secondUser, secondRef, default);
        Assert.NotNull(slot.ReservationId);
        var enrolled = await Service(second).ConfirmAsync(course.Id, secondUser, secondRef, DateTime.UtcNow, true, default);
        Assert.Equal(enrolled.Id, (await Service(second).ConfirmAsync(course.Id, secondUser, secondRef, DateTime.UtcNow, true, default)).Id);
        await using var other = Db();
        Assert.Equal(1, await other.CourseEnrollments.CountAsync(x => x.CourseId == course.Id));
        Assert.Equal(0, await other.CourseSlotReservations.CountAsync(x => x.CourseId == course.Id &&
            x.Status == SlotReservationStatus.Reserved));
        Assert.Equal("reservation_not_fulfillable", (await Assert.ThrowsAsync<ContentRuleException>(() =>
            Service(second).ConfirmAsync(course.Id, Guid.NewGuid(), firstRef, DateTime.UtcNow, true, default))).Code);
    }

    [Fact]
    public async Task VerifiedBeforeDeadline_RemainsFulfillableAfterDeadline()
    {
        if (Connection is null) return;
        var course = await CourseAsync(100000, 1);
        var clock = new MutableClock(DateTimeOffset.UtcNow);
        var reference = Guid.NewGuid();
        var user = Guid.NewGuid();
        await using var db = Db();
        await Service(db, clock).ReserveAsync(course.Id, user, reference, default);
        var bankTransferAt = clock.GetUtcNow().UtcDateTime.AddMinutes(1);
        clock.Advance(TimeSpan.FromMinutes(20));
        var enrolled = await Service(db, clock).ConfirmAsync(course.Id, user, reference, bankTransferAt, true, default);
        Assert.Equal(user, enrolled.UserId);
        Assert.Equal(SlotReservationStatus.Confirmed,
            (await db.CourseSlotReservations.SingleAsync(x => x.CheckoutReference == reference)).Status);
    }

    [Fact]
    public async Task LearningAccess_DeniesNonOwnerAndUnpublishedCourse()
    {
        if (Connection is null) return;
        var course = await CourseAsync(0, null);
        var user = Guid.NewGuid();
        await using var db = Db();
        await Service(db).FreeEnrollAsync(course.Id, user, default);
        Assert.Null(await Service(db).StatusAsync(course.Id, Guid.NewGuid(), default));
        Assert.Empty((await Service(db).MineAsync(Guid.NewGuid(), null, 1, 20, default)).Items);
        Assert.Contains((await Service(db).MineAsync(user, null, 1, 20, default)).Items,
            x => x.CourseId == course.Id);
        Assert.Equal("course_not_found", (await Assert.ThrowsAsync<ContentRuleException>(() =>
            Service(db).LessonAsync(course.Id, Guid.NewGuid(), Guid.NewGuid(), default))).Code);
    }

    [Fact]
    public async Task EnrolledOwnerCanReadPublishedLesson_WithoutRawStorageKeys()
    {
        if (Connection is null) return;
        var course = await CourseAsync(0, null);
        var user = Guid.NewGuid();
        var mediaPrefix = "private/" + Guid.NewGuid().ToString("N") + "/";
        await using var db = Db();
        var module = new CourseModule
        {
            CourseId = course.Id, Title = "Module", SortOrder = 1,
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        };
        var lesson = new Lesson
        {
            Module = module, Title = "Lesson", TextContent = "Private markdown",
            Status = LessonStatus.Published, SortOrder = 1,
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        };
        lesson.Videos.Add(new LessonVideo
        {
            Lesson = lesson, ObjectKey = mediaPrefix + "video.mp4", FileName = "video.mp4",
            ContentType = "video/mp4", FileSize = 128, SortOrder = 1, CreatedAt = DateTime.UtcNow
        });
        lesson.Resources.Add(new LessonResource
        {
            Lesson = lesson, ObjectKey = mediaPrefix + "file.pdf", FileName = "file.pdf",
            ContentType = "application/pdf", FileSize = 256, CreatedAt = DateTime.UtcNow
        });
        db.Lessons.Add(lesson);
        await db.SaveChangesAsync();
        await Service(db).FreeEnrollAsync(course.Id, user, default);
        var result = await Service(db).LessonAsync(course.Id, lesson.Id, user, default);
        Assert.Equal("Private markdown", result.TextContent);
        Assert.Single(result.Videos);
        Assert.Single(result.Attachments);
        Assert.DoesNotContain("private/", result.Videos[0].Url);
        Assert.DoesNotContain("private/", result.Attachments[0].Url);
        await using var outsider = Db();
        Assert.Equal("course_not_found", (await Assert.ThrowsAsync<ContentRuleException>(() =>
            Service(outsider).LessonAsync(course.Id, lesson.Id, Guid.NewGuid(), default))).Code);
    }

    [Fact]
    public async Task CapacityCannotBeReducedBelowHeldSlotsOrConfirmedEnrollments()
    {
        if (Connection is null) return;
        var course = await CourseAsync(100000, 2);
        await using var db = Db();
        await Service(db).ReserveAsync(course.Id, Guid.NewGuid(), Guid.NewGuid(), default);
        await Service(db).ReserveAsync(course.Id, Guid.NewGuid(), Guid.NewGuid(), default);
        var handler = new UpdateCoursePricingHandler(new CourseRepository(db), new UnitOfWork(db), TimeProvider.System);
        Assert.Equal("capacity_below_occupied", (await Assert.ThrowsAsync<ContentRuleException>(() =>
            handler.Handle(new UpdateCoursePricingCommand(course.Id, 200000, 1), default))).Code);
        await db.Entry(await db.Courses.SingleAsync(x => x.Id == course.Id)).ReloadAsync();
        Assert.Equal(2, (await handler.Handle(new UpdateCoursePricingCommand(course.Id, 200000, 2), default)).Capacity);
    }

    private sealed class NoMedia : ILessonMediaStore
    {
        public Task<string> PresignUploadAsync(string key, string contentType, TimeSpan lifetime, CancellationToken ct) =>
            throw new NotSupportedException();
        public Task<StoredThumbnail?> HeadAsync(string key, CancellationToken ct) => throw new NotSupportedException();
        public Task<string> PresignReadAsync(string key, TimeSpan lifetime, CancellationToken ct) =>
            Task.FromResult("https://signed.example.test/media?sig=short-lived");
        public Task DeleteAsync(string key, CancellationToken ct) => throw new NotSupportedException();
    }

    private sealed class MutableClock(DateTimeOffset now) : TimeProvider
    {
        private DateTimeOffset _now = now;
        public override DateTimeOffset GetUtcNow() => _now;
        public void Advance(TimeSpan duration) => _now = _now.Add(duration);
    }
}
