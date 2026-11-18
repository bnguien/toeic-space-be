using Microsoft.EntityFrameworkCore;
using ToeicSpace.Content.Application.Common.Pagination;
using ToeicSpace.Content.Application.Interfaces.Repositories;
using ToeicSpace.Content.Domain.Entities;
using ToeicSpace.Content.Domain.Enums;

namespace ToeicSpace.Content.Infrastructure.Persistence.Repositories;

public sealed class PublicCourseRepository(ContentDbContext db) : IPublicCourseRepository
{
    public async Task<IReadOnlyDictionary<Guid, int>> GetRemainingSeatsAsync(IReadOnlyCollection<Guid> courseIds, CancellationToken ct)
    {
        if (courseIds.Count == 0) return new Dictionary<Guid, int>();
        var courses = await db.Courses.AsNoTracking().Where(x => courseIds.Contains(x.Id) && x.Capacity != null)
            .Select(x => new { x.Id, x.Capacity }).ToListAsync(ct);
        var ids = courses.Select(x => x.Id).ToArray();
        var enrolled = await db.CourseEnrollments.AsNoTracking().Where(x => ids.Contains(x.CourseId))
            .GroupBy(x => x.CourseId).Select(x => new { CourseId = x.Key, Count = x.Count() }).ToListAsync(ct);
        var reserved = await db.CourseSlotReservations.AsNoTracking().Where(x => ids.Contains(x.CourseId) &&
                x.Status == SlotReservationStatus.Reserved)
            .GroupBy(x => x.CourseId).Select(x => new { CourseId = x.Key, Count = x.Count() }).ToListAsync(ct);
        var enrollmentCounts = enrolled.ToDictionary(x => x.CourseId, x => x.Count);
        var reservationCounts = reserved.ToDictionary(x => x.CourseId, x => x.Count);
        return courses.ToDictionary(x => x.Id, x => Math.Max(0, x.Capacity!.Value -
            enrollmentCounts.GetValueOrDefault(x.Id) - reservationCounts.GetValueOrDefault(x.Id)));
    }
    public Task<PagedResult<Course>> BrowseAsync(string? search, CourseExamType? examType, CourseLevel? level,
        int? targetScore, int page, int pageSize, CancellationToken ct) =>
        PersistenceExceptionTranslator.ExecuteAsync(async () =>
        {
            var query = db.Courses.AsNoTracking().Where(x => x.Status == CourseStatus.Published);
            if (!string.IsNullOrWhiteSpace(search))
            {
                var term = search.Trim();
                query = query.Where(x => x.Title.Contains(term) || x.Code.Contains(term));
            }
            if (examType is { } exam) query = query.Where(x => x.ExamType == exam);
            if (level is { } value) query = query.Where(x => x.Level == value);
            if (targetScore is { } score) query = query.Where(x => x.TargetScore == score);
            var count = await query.CountAsync(ct);
            var items = await query.OrderByDescending(x => x.UpdatedAt).ThenBy(x => x.Id)
                .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
            return new PagedResult<Course>(items, page, pageSize, count);
        });

    public Task<Course?> GetPublishedAsync(Guid id, CancellationToken ct) =>
        PersistenceExceptionTranslator.ExecuteAsync(() => db.Courses.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id && x.Status == CourseStatus.Published, ct));

    public Task<IReadOnlyList<CourseModule>> GetCurriculumAsync(Guid courseId, CancellationToken ct) =>
        PersistenceExceptionTranslator.ExecuteAsync(async () =>
            (IReadOnlyList<CourseModule>)await db.CourseModules.AsNoTracking().Where(x => x.CourseId == courseId)
                .Include(x => x.Lessons.Where(l => l.Status == LessonStatus.Published))
                .AsSplitQuery().OrderBy(x => x.SortOrder).ThenBy(x => x.Id).ToListAsync(ct));

    public Task<Lesson?> GetPreviewAsync(Guid courseId, Guid lessonId, CancellationToken ct) =>
        PersistenceExceptionTranslator.ExecuteAsync(() => db.Lessons.AsNoTracking()
            .Include(x => x.Videos).Include(x => x.Resources).AsSplitQuery()
            .FirstOrDefaultAsync(x => x.Id == lessonId && x.Status == LessonStatus.Published
                && x.IsFreePreview && x.Module.CourseId == courseId
                && x.Module.Course.Status == CourseStatus.Published, ct));
}
