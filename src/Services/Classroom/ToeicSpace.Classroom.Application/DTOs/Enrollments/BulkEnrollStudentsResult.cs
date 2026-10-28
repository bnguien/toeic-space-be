namespace ToeicSpace.Classroom.Application.DTOs.Enrollments;

public sealed record BulkEnrollStudentResult(int Index, Guid StudentId, Guid? EnrollmentId,
    string? Code, string? Message);

public sealed record BulkEnrollStudentsResult(int TotalCount, int EnrolledCount, int FailedCount,
    IReadOnlyList<BulkEnrollStudentResult> Items);
