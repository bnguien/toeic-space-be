using ToeicSpace.Classroom.Application.DTOs.Facilities;

namespace ToeicSpace.Classroom.Application.Features.Centers.Commands.CreateCenter;

public sealed record CreateCenterCommand(string Name, string Address, string? TimeZoneId = null) : IRequest<CenterDto>;
