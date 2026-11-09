using ToeicSpace.Content.Domain.Entities;

namespace ToeicSpace.Content.Application.Interfaces.Repositories;

public interface ICourseModuleRepository
{
    Task<IReadOnlyList<CourseModule>> ListAsync(Guid courseId, bool tracking, CancellationToken ct);
    Task<CourseModule?> GetAsync(Guid courseId, Guid moduleId, CancellationToken ct);
    Task<bool> HasLessonsAsync(Guid moduleId, CancellationToken ct);
    Task ShiftOrdersAsync(Guid courseId, int offset, CancellationToken ct);
    void Add(CourseModule module);
    void Remove(CourseModule module);
}
