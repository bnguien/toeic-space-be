using ToeicSpace.Classroom.Application.Common.Pagination;
using ToeicSpace.Classroom.Application.DTOs.Facilities;
using ToeicSpace.Classroom.Application.Interfaces.Repositories;
using ToeicSpace.Classroom.Domain.Exceptions;

namespace ToeicSpace.Classroom.Application.Features.Centers.Queries.ListCenters;

public sealed class ListCentersHandler(IFacilityRepository facilities)
    : IRequestHandler<ListCentersQuery, PagedResult<CenterDto>>
{
    public async Task<PagedResult<CenterDto>> Handle(ListCentersQuery request, CancellationToken ct)
    {
        if (request.Page < 1 || request.PageSize is < 1 or > 100 || request.Page > int.MaxValue / request.PageSize ||
            request.Status is { } status && !Enum.IsDefined(status))
            throw new ClassRuleException("invalid_filter", "Invalid pagination or status.");
        var page = await facilities.CentersAsync(request.Search, request.Status, request.Page, request.PageSize, ct);
        return new PagedResult<CenterDto>(page.Items.Select(CenterDto.From).ToArray(), page.Page, page.PageSize, page.TotalCount);
    }
}
