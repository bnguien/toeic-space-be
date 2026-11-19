using System.Text.Json.Serialization;
using ToeicSpace.Classroom.Application.DTOs.Classes;

namespace ToeicSpace.Classroom.Application.Features.Classes.Commands.AssignTeacher;

public sealed record AssignTeacherCommand(Guid TeacherId) : IRequest<ClassDetailDto>
{
    [JsonIgnore] public Guid ClassId { get; set; }

    [JsonIgnore] public Guid AdminId { get; set; }
}