using ToeicSpace.Classroom.Application.DTOs.Scheduling;

namespace ToeicSpace.Classroom.Application.Interfaces.Services;

public interface IStudentConflictReadService
{
    Task<StudentConflictPreview> GetMineAsync(Guid studentId, DateTime from, DateTime to,
        ConflictDetailsRequest? details, CancellationToken ct);
}
