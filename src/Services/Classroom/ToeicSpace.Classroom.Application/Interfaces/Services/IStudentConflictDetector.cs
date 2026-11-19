using ToeicSpace.Classroom.Application.DTOs.Scheduling;

namespace ToeicSpace.Classroom.Application.Interfaces.Services;

public interface IStudentConflictDetector
{
    Task<StudentConflictPreview> PreviewAsync(IReadOnlyList<StudentConflictSession> proposed,
        IReadOnlyCollection<Guid>? enrollingStudents, ConflictDetailsRequest? details, CancellationToken ct);
}
