using ToeicSpace.Classroom.Application.DTOs.Facilities;
using ToeicSpace.Classroom.Application.Interfaces.Repositories;
using ToeicSpace.Classroom.Domain.Exceptions;

namespace ToeicSpace.Classroom.Application.Features.Centers.Queries.GetCenter;

public sealed class GetCenterHandler(IFacilityRepository facilities) : IRequestHandler<GetCenterQuery, CenterDto>
{
    public async Task<CenterDto> Handle(GetCenterQuery request, CancellationToken ct) =>
        CenterDto.From(await facilities.CenterAsync(request.Id, ct)
            ?? throw new ClassRuleException("center_not_found", "Center not found.", 404));
}
