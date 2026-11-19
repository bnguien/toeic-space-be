using System.Text.Json.Serialization;
using ToeicSpace.Classroom.Application.DTOs.Classes;

namespace ToeicSpace.Classroom.Application.Features.Classes.Commands.SetDefaultRoom;

public sealed record SetDefaultRoomCommand(Guid? RoomId, string? Reason, bool ApplyToFutureSessions = false) : IRequest<ClassDetailDto>
{
    [JsonIgnore] public Guid ClassId { get; set; }
    [JsonIgnore] public Guid AdminId { get; set; }
}
