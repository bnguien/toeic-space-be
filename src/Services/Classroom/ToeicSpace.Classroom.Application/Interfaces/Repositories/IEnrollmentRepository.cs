using ToeicSpace.Classroom.Application.Common.Pagination;
using ToeicSpace.Classroom.Domain.Entities;
using ToeicSpace.Classroom.Domain.Enums;

namespace ToeicSpace.Classroom.Application.Interfaces.Repositories;

public sealed record EnrollmentWithStudent(ClassEnrollment Enrollment, string? StudentName, string? StudentEmail);

public interface IEnrollmentRepository
{
    Task<bool> HasActiveAsync(Guid classId, Guid studentId, CancellationToken ct);
    Task<bool> HasHistoryAsync(Guid classId, CancellationToken ct);
    Task<ClassEnrollment?> GetByIdAsync(Guid classId, Guid enrollmentId, CancellationToken ct);
    Task<PagedResult<EnrollmentWithStudent>> ListAsync(Guid classId, EnrollmentStatus? status, Guid? studentId,
        int page, int pageSize, CancellationToken ct);
    void Add(ClassEnrollment enrollment);
}
