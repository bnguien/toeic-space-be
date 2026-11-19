using ToeicSpace.Classroom.Application.DTOs.Facilities;
using ToeicSpace.Classroom.Application.Interfaces.Repositories;
using ToeicSpace.Classroom.Domain.Exceptions;

namespace ToeicSpace.Classroom.Application.Features.Classes.Queries.GetRoomHistory;

public sealed class GetRoomHistoryHandler(IClassRepository classes, IFacilityRepository facilities)
    : IRequestHandler<GetRoomHistoryQuery, IReadOnlyList<ClassRoomChangeDto>>
{
    public async Task<IReadOnlyList<ClassRoomChangeDto>> Handle(GetRoomHistoryQuery request, CancellationToken ct)
    {
        if (await classes.GetByIdAsync(request.ClassId, false, ct) is null) throw ClassRuleException.NotFound();
        return (await facilities.RoomHistoryAsync(request.ClassId, ct)).Select(ClassRoomChangeDto.From).ToArray();
    }
}
