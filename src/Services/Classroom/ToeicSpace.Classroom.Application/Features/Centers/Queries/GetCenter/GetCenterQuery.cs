using ToeicSpace.Classroom.Application.DTOs.Facilities;

namespace ToeicSpace.Classroom.Application.Features.Centers.Queries.GetCenter;

public sealed record GetCenterQuery(Guid Id) : IRequest<CenterDto>;
