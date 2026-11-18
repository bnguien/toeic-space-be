using ToeicSpace.Classroom.Application.DTOs.Scheduling;

namespace ToeicSpace.Classroom.Application.Interfaces.Services;

public interface IEnrollmentConflictPreviewService
{
    Task<StudentConflictPreview> PreviewAsync(Guid classId, IReadOnlyList<Guid> studentIds,
        ConflictDetailsRequest? details, CancellationToken ct);
}
