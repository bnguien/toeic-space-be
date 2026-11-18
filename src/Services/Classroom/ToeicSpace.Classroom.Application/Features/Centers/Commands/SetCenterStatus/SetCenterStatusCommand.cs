using ToeicSpace.Classroom.Application.DTOs.Facilities;
using ToeicSpace.Classroom.Domain.Enums;

namespace ToeicSpace.Classroom.Application.Features.Centers.Commands.SetCenterStatus;

public sealed record SetCenterStatusCommand(Guid Id, FacilityStatus Status) : IRequest<CenterDto>;
