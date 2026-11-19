namespace ToeicSpace.Classroom.Application.DTOs.Enrollments;

public sealed record ImportStudentRowResult(int RowNumber, Guid? StudentId, Guid? EnrollmentId,
    string? Code, string? Message);

public sealed record ImportStudentsResult(int TotalRows, int ImportedCount, int FailedCount,
    IReadOnlyList<ImportStudentRowResult> Rows);
