using ToeicSpace.Classroom.Domain.Enums;

namespace ToeicSpace.Classroom.Application.Analytics.Common;

/// <summary>One class, without its code, name or people.</summary>
public sealed record ClassFact(
    Guid ClassId,
    ClassStatus Status,
    DateTime CreatedAt,
    DateOnly? StartDate);

/// <summary>
/// One enrollment. <paramref name="Status"/> is the current state: Active only while the row is not withdrawn
/// (Status Active and no WithdrawnAt), otherwise Withdrawn. The student ID is used for distinct counts only.
/// </summary>
public sealed record EnrollmentFact(
    Guid ClassId,
    Guid StudentId,
    EnrollmentStatus Status,
    DateTime EnrolledAt,
    DateOnly? ClassStartDate);

/// <summary>A current (not unassigned) teacher assignment.</summary>
public sealed record TeacherAssignmentFact(
    Guid TeacherId,
    Guid ClassId);

/// <param name="CreatedFrom">Inclusive lower bound of <c>CreatedAt</c>.</param>
/// <param name="CreatedToExclusive">Exclusive upper bound of <c>CreatedAt</c>.</param>
/// <param name="StartFrom">Inclusive lower bound of <c>StartDate</c>; classes without a start date are skipped.</param>
/// <param name="StartTo">Inclusive upper bound of <c>StartDate</c>.</param>
/// <param name="Statuses">Only these class statuses; null means every status but Deleted.</param>
public sealed record ClassFactFilter(
    DateTime? CreatedFrom = null,
    DateTime? CreatedToExclusive = null,
    DateOnly? StartFrom = null,
    DateOnly? StartTo = null,
    IReadOnlyCollection<ClassStatus>? Statuses = null);

/// <param name="EnrolledFrom">Inclusive lower bound of <c>EnrolledAt</c>.</param>
/// <param name="EnrolledToExclusive">Exclusive upper bound of <c>EnrolledAt</c>.</param>
/// <param name="ClassStartFrom">Inclusive lower bound of the class <c>StartDate</c>.</param>
/// <param name="ClassStartTo">Inclusive upper bound of the class <c>StartDate</c>.</param>
/// <param name="Status">Current enrollment status (see <see cref="EnrollmentFact"/>).</param>
public sealed record EnrollmentFactFilter(
    DateTime? EnrolledFrom = null,
    DateTime? EnrolledToExclusive = null,
    DateOnly? ClassStartFrom = null,
    DateOnly? ClassStartTo = null,
    Guid? ClassId = null,
    EnrollmentStatus? Status = null);
