using System.Text.Json.Serialization;
using ToeicSpace.Classroom.Application.DTOs.Classes;

namespace ToeicSpace.Classroom.Application.Features.Classes.Commands.CreateClass;

public sealed record CreateClassCommand(
    string Code,
    string Name,
    string? Description,
    DateOnly? StartDate,
    DateOnly? EndDate)
    : IRequest<ClassDetailDto>
{
    [JsonIgnore] public Guid AdminId { get; set; }
}