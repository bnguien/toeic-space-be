namespace ToeicSpace.Classroom.Application.Interfaces.Services;

public sealed record EnrollmentImportRow(int RowNumber, string? StudentId, string? Email, string? ErrorCode = null);

public interface IEnrollmentImportReader
{
    Task<IReadOnlyList<EnrollmentImportRow>> ReadAsync(Stream file, string fileName, CancellationToken ct);
}
