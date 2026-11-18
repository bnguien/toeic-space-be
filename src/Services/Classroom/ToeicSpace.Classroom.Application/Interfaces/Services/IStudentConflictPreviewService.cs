using ToeicSpace.Classroom.Application.DTOs.Scheduling;

namespace ToeicSpace.Classroom.Application.Interfaces.Services;

public interface IStudentConflictPreviewService
{
    Task<StudentConflictPreview> CreateSessionAsync(SessionInput input, Guid actor, bool admin,
        ConflictDetailsRequest? details, CancellationToken ct);
    Task<StudentConflictPreview> UpdateSessionAsync(Guid id, SessionEdit input, Guid actor, bool admin,
        ConflictDetailsRequest? details, CancellationToken ct);
    Task<StudentConflictPreview> CreateScheduleAsync(ScheduleInput input, ConflictDetailsRequest? details, CancellationToken ct);
    Task<StudentConflictPreview> UpdateScheduleAsync(Guid id, ScheduleInput input, int expectedVersion,
        ConflictDetailsRequest? details, CancellationToken ct);
    Task<StudentConflictPreview> GenerateSessionsAsync(Guid classId, GenerateSessionsInput input,
        ConflictDetailsRequest? details, CancellationToken ct);
}
