using System.Text.Json.Serialization;
using ToeicSpace.Classroom.Application.DTOs.Enrollments;

namespace ToeicSpace.Classroom.Application.Features.Enrollments.Commands.EnrollStudent;

public sealed record EnrollStudentCommand(Guid StudentId) : IRequest<ClassEnrollmentDto>
{
    [JsonIgnore] public Guid ClassId { get; set; }
    [JsonIgnore] public Guid AdminId { get; set; }
}
