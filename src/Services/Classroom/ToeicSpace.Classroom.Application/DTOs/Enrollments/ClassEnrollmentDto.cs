using ToeicSpace.Classroom.Domain.Enums;

namespace ToeicSpace.Classroom.Application.DTOs.Enrollments;

public sealed record ClassEnrollmentDto(
    Guid Id, Guid ClassId, Guid StudentId, string? StudentName, string? StudentEmail,
    EnrollmentStatus Status, DateTime EnrolledAt, Guid EnrolledBy,
    DateTime? WithdrawnAt, Guid? WithdrawnBy);
