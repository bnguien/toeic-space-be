using ToeicSpace.Classroom.Application.Common.Pagination;
using ToeicSpace.Classroom.Application.DTOs.Facilities;
using ToeicSpace.Classroom.Application.Interfaces.Repositories;
using ToeicSpace.Classroom.Domain.Exceptions;

namespace ToeicSpace.Classroom.Application.Features.Rooms.Queries.ListRooms;

public sealed class ListRoomsHandler(IFacilityRepository facilities)
    : IRequestHandler<ListRoomsQuery, PagedResult<RoomDto>>
{
    public async Task<PagedResult<RoomDto>> Handle(ListRoomsQuery request, CancellationToken ct)
    {
        if (request.Page < 1 || request.PageSize is < 1 or > 100 || request.Page > int.MaxValue / request.PageSize ||
            request.Status is { } status && !Enum.IsDefined(status))
            throw new ClassRuleException("invalid_filter", "Invalid pagination or status.");
        var page = await facilities.RoomsAsync(request.CenterId, request.Search, request.Status,
            request.Page, request.PageSize, ct);
        return new PagedResult<RoomDto>(page.Items.Select(RoomDto.From).ToArray(), page.Page, page.PageSize, page.TotalCount);
    }
}
