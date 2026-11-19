using ToeicSpace.Classroom.Application.DTOs.Facilities;
using ToeicSpace.Classroom.Domain.Enums;

namespace ToeicSpace.Classroom.Application.Features.Rooms.Commands.SetRoomStatus;

public sealed record SetRoomStatusCommand(Guid Id, FacilityStatus Status) : IRequest<RoomDto>;
