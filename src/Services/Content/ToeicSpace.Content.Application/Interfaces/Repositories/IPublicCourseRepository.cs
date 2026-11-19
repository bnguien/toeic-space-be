using ToeicSpace.Content.Application.Common.Pagination;
using ToeicSpace.Content.Domain.Entities;
using ToeicSpace.Content.Domain.Enums;

namespace ToeicSpace.Content.Application.Interfaces.Repositories;

public interface IPublicCourseRepository
{
    Task<IReadOnlyDictionary<Guid, int>> GetRemainingSeatsAsync(IReadOnlyCollection<Guid> courseIds, CancellationToken ct);
    Task<PagedResult<Course>> BrowseAsync(string? search, CourseExamType? examType, CourseLevel? level,
        int? targetScore, int page, int pageSize, CancellationToken ct);
    Task<Course?> GetPublishedAsync(Guid id, CancellationToken ct);
    Task<IReadOnlyList<CourseModule>> GetCurriculumAsync(Guid courseId, CancellationToken ct);
    Task<Lesson?> GetPreviewAsync(Guid courseId, Guid lessonId, CancellationToken ct);
}
