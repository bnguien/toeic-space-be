using Microsoft.EntityFrameworkCore;
using ToeicSpace.Content.Application.DTOs.Dictation;
using ToeicSpace.Content.Application.Interfaces.Repositories;
using ToeicSpace.Content.Domain.Entities;

namespace ToeicSpace.Content.Infrastructure.Persistence.Repositories;

public sealed class DictationCatalogRepository(ContentDbContext db) : IDictationCatalogRepository
{
    public Task<IReadOnlyList<DictationCourse>> GetCoursesAsync(IReadOnlyCollection<string> packs, CancellationToken ct) =>
        PersistenceExceptionTranslator.ExecuteAsync<IReadOnlyList<DictationCourse>>(async () =>
            await db.DictationCourses.AsNoTracking()
                .Where(x => x.IsActive && packs.Contains(x.Pack))
                .OrderBy(x => x.Level).ThenBy(x => x.Order).ThenBy(x => x.Id)
                .ToListAsync(ct));

    public Task<DictationCourse?> GetCourseAsync(int courseId, CancellationToken ct) =>
        PersistenceExceptionTranslator.ExecuteAsync(() =>
            db.DictationCourses.AsNoTracking().FirstOrDefaultAsync(x => x.Id == courseId && x.IsActive, ct));

    public Task<IReadOnlyList<(int ItemId, int CourseId)>> GetItemIdsAsync(IReadOnlyCollection<int> courseIds,
        CancellationToken ct) =>
        PersistenceExceptionTranslator.ExecuteAsync<IReadOnlyList<(int, int)>>(async () =>
            (await db.DictationItems.AsNoTracking()
                .Where(x => x.IsActive && courseIds.Contains(x.CourseId))
                .OrderBy(x => x.Order).ThenBy(x => x.Id)
                .Select(x => new { x.Id, x.CourseId })
                .ToListAsync(ct))
            .Select(x => (x.Id, x.CourseId)).ToList());

    public Task<IReadOnlyList<DictationItem>> GetCourseItemsAsync(int courseId, CancellationToken ct) =>
        PersistenceExceptionTranslator.ExecuteAsync<IReadOnlyList<DictationItem>>(async () =>
            await db.DictationItems.AsNoTracking()
                .Where(x => x.CourseId == courseId && x.IsActive)
                .OrderBy(x => x.Order).ThenBy(x => x.Id)
                .ToListAsync(ct));

    public Task<DictationItem?> GetItemAsync(int itemId, CancellationToken ct) =>
        PersistenceExceptionTranslator.ExecuteAsync(() =>
            db.DictationItems.AsNoTracking().FirstOrDefaultAsync(x => x.Id == itemId && x.IsActive, ct));

    public Task<IReadOnlyList<DictationItem>> GetItemsAsync(IReadOnlyCollection<int> itemIds, CancellationToken ct) =>
        PersistenceExceptionTranslator.ExecuteAsync<IReadOnlyList<DictationItem>>(async () =>
            await db.DictationItems.AsNoTracking().Where(x => itemIds.Contains(x.Id) && x.IsActive).ToListAsync(ct));

    public Task<IReadOnlyList<DictationItem>> GetFollowingItemsAsync(DictationItem item, int count, CancellationToken ct) =>
        PersistenceExceptionTranslator.ExecuteAsync<IReadOnlyList<DictationItem>>(async () =>
            await db.DictationItems.AsNoTracking()
                .Where(x => x.CourseId == item.CourseId && x.IsActive
                    && (x.Order > item.Order || (x.Order == item.Order && x.Id > item.Id)))
                .OrderBy(x => x.Order).ThenBy(x => x.Id)
                .Take(count)
                .ToListAsync(ct));

    public Task<int> CountActiveItemsAsync(CancellationToken ct) =>
        PersistenceExceptionTranslator.ExecuteAsync(() => db.DictationItems.CountAsync(x => x.IsActive, ct));

    public Task<IReadOnlyList<DictationPackSummaryDto>> GetPackSummariesAsync(CancellationToken ct) =>
        PersistenceExceptionTranslator.ExecuteAsync<IReadOnlyList<DictationPackSummaryDto>>(async () =>
            (await db.DictationCourses.AsNoTracking()
                .Where(x => x.IsActive)
                .GroupBy(x => x.Pack)
                .Select(g => new { Pack = g.Key, Courses = g.Count(), Items = g.Sum(x => x.ItemCount) })
                .ToListAsync(ct))
            .OrderBy(x => x.Pack, StringComparer.Ordinal)
            .Select(x => new DictationPackSummaryDto(x.Pack, x.Courses, x.Items)).ToList());
}
