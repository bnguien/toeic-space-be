using ToeicSpace.Classroom.Application.DTOs.Enrollments;
using ToeicSpace.Classroom.Domain.Entities;

namespace ToeicSpace.Classroom.Application.Mappings;

public static class EnrollmentMappingExtensions
{
    public static ClassEnrollmentDto ToDto(this ClassEnrollment x, string? studentName, string? studentEmail) =>
        new(x.Id, x.ClassId, x.StudentId, studentName, studentEmail,
            x.Status, x.EnrolledAt, x.EnrolledBy, x.WithdrawnAt, x.WithdrawnBy);
}
