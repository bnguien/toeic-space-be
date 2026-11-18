using ToeicSpace.Classroom.Application.DTOs.Facilities;

namespace ToeicSpace.Classroom.Application.Features.Centers.Commands.UpdateCenter;

public sealed record UpdateCenterCommand(Guid Id, string Name, string Address, string TimeZoneId) : IRequest<CenterDto>;
