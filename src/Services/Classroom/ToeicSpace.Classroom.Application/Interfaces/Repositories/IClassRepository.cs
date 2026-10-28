using ToeicSpace.Classroom.Application.Common.Pagination;
using ToeicSpace.Classroom.Application.Features.Classes.Queries.GetClasses;
using ToeicSpace.Classroom.Domain.Entities;
using ToeicSpace.Classroom.Domain.Enums;

namespace ToeicSpace.Classroom.Application.Interfaces.Repositories;

public sealed record StudentClassView(Class Class, string? TeacherName);

public interface IClassRepository
{
    Task<bool> CodeExistsAsync(string code, CancellationToken cancellationToken);
    Task<Class?> GetByIdAsync(Guid id, bool includeDeleted, CancellationToken cancellationToken);
    Task<Class?> GetDetailAsync(Guid id, CancellationToken cancellationToken);
    Task<PagedResult<Class>> SearchAsync(ClassSearchCriteria criteria, CancellationToken cancellationToken);
    Task<PagedResult<Class>> GetDeletedAsync(string? search, int page, int pageSize, CancellationToken cancellationToken);
    Task<PagedResult<Class>> GetTeacherClassesAsync(Guid teacherId, int page, int pageSize, CancellationToken cancellationToken);
    Task<PagedResult<StudentClassView>> GetStudentClassesAsync(Guid studentId, string? search, ClassStatus? status,
        int page, int pageSize, CancellationToken cancellationToken);
    Task<StudentClassView?> GetStudentDetailAsync(Guid studentId, Guid classId, CancellationToken cancellationToken);
    Task<IReadOnlyList<Guid>> GetDraftIdsReadyToStartAsync(DateOnly today, CancellationToken cancellationToken);
    void Add(Class entity);
    void Add(ClassInstructor entity);
    void Remove(Class entity);
}
