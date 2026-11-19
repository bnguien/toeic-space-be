using System.Text.Json.Serialization;
using ToeicSpace.Classroom.Application.DTOs.Classes;
using ToeicSpace.Classroom.Domain.Entities;
using ToeicSpace.Classroom.Domain.Enums;

namespace ToeicSpace.Classroom.Application.Features.Classes.Commands.ChangeClassStatus;

public sealed record ChangeClassStatusCommand(ClassStatus Status) : IRequest<ClassDetailDto>
{
    [JsonIgnore]
    public Guid ClassId { get; set; }

    [JsonIgnore]
    public Guid AdminId { get; set; }
}
