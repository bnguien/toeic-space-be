using ToeicSpace.Classroom.Application.DTOs.Facilities;
using ToeicSpace.Classroom.Application.Interfaces;
using ToeicSpace.Classroom.Application.Interfaces.Repositories;
using ToeicSpace.Classroom.Domain.Exceptions;

namespace ToeicSpace.Classroom.Application.Features.Centers.Commands.SetCenterStatus;

public sealed class SetCenterStatusHandler(IFacilityRepository facilities, IUnitOfWork unitOfWork, TimeProvider clock)
    : IRequestHandler<SetCenterStatusCommand, CenterDto>
{
    public async Task<CenterDto> Handle(SetCenterStatusCommand request, CancellationToken ct)
    {
        var center = await facilities.CenterAsync(request.Id, ct)
            ?? throw new ClassRuleException("center_not_found", "Center not found.", 404);
        center.Status = request.Status;
        center.UpdatedAt = clock.GetUtcNow().UtcDateTime;
        await unitOfWork.SaveChangesAsync(ct);
        return CenterDto.From(center);
    }
}
