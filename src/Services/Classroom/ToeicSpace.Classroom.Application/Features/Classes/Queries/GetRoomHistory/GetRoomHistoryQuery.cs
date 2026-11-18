using ToeicSpace.Classroom.Application.DTOs.Facilities;

namespace ToeicSpace.Classroom.Application.Features.Classes.Queries.GetRoomHistory;

public sealed record GetRoomHistoryQuery(Guid ClassId) : IRequest<IReadOnlyList<ClassRoomChangeDto>>;
