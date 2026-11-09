using ToeicSpace.Content.Application.Common.Pagination;
using ToeicSpace.Content.Domain.Entities;
using ToeicSpace.Content.Domain.Enums;

namespace ToeicSpace.Content.Application.Interfaces.Repositories;

public interface ICourseRepository
{
    Task<Course?> GetAsync(Guid id, CancellationToken ct);
    Task<bool> CodeExistsAsync(string code, Guid? excludeId, CancellationToken ct);
    Task<bool> SlugExistsAsync(string slug, Guid? excludeId, CancellationToken ct);
    Task<PagedResult<Course>> SearchAsync(string? search, CourseStatus? status, CourseLevel? level,
        CourseExamType? examType, int page, int pageSize, CancellationToken ct);
    void Add(Course course);
}
