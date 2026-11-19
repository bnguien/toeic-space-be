using ToeicSpace.Content.Domain.Entities;

namespace ToeicSpace.Content.Application.Interfaces.Repositories;

/// <summary>Read-only access to active dictation courses and sentences.</summary>
public interface IDictationCatalogRepository
{
    /// <summary>Active courses of the packs, ordered by level, order, id.</summary>
    Task<IReadOnlyList<DictationCourse>> GetCoursesAsync(IReadOnlyCollection<string> packs, CancellationToken ct);
    Task<DictationCourse?> GetCourseAsync(int courseId, CancellationToken ct);
    /// <summary>(item, course) ids of the active sentences of the courses, in sentence order.</summary>
    Task<IReadOnlyList<(int ItemId, int CourseId)>> GetItemIdsAsync(IReadOnlyCollection<int> courseIds,
        CancellationToken ct);
    /// <summary>Active sentences of a course in order.</summary>
    Task<IReadOnlyList<DictationItem>> GetCourseItemsAsync(int courseId, CancellationToken ct);
    Task<DictationItem?> GetItemAsync(int itemId, CancellationToken ct);
    Task<IReadOnlyList<DictationItem>> GetItemsAsync(IReadOnlyCollection<int> itemIds, CancellationToken ct);
    /// <summary>The next active sentences of the same course.</summary>
    Task<IReadOnlyList<DictationItem>> GetFollowingItemsAsync(DictationItem item, int count, CancellationToken ct);
    Task<int> CountActiveItemsAsync(CancellationToken ct);
    /// <summary>Packs with active courses: course and sentence counts.</summary>
    Task<IReadOnlyList<ToeicSpace.Content.Application.DTOs.Dictation.DictationPackSummaryDto>> GetPackSummariesAsync(
        CancellationToken ct);
}
