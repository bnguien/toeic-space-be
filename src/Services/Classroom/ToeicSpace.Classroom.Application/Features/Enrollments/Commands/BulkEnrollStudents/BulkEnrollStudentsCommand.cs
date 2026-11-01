using System.Text.Json.Serialization;
using ToeicSpace.Classroom.Application.DTOs.Enrollments;

namespace ToeicSpace.Classroom.Application.Features.Enrollments.Commands.BulkEnrollStudents;

public sealed record BulkEnrollStudentsCommand(IReadOnlyList<Guid> StudentIds) : IRequest<BulkEnrollStudentsResult>
{
    [JsonIgnore] public Guid ClassId { get; set; }
    [JsonIgnore] public Guid AdminId { get; set; }
}
