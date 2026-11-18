using ToeicSpace.Classroom.Application.DTOs.Facilities;
using ToeicSpace.Classroom.Application.Interfaces;
using ToeicSpace.Classroom.Application.Interfaces.Repositories;
using ToeicSpace.Classroom.Domain.Entities;
using ToeicSpace.Classroom.Domain.Exceptions;

namespace ToeicSpace.Classroom.Application.Features.Centers.Commands.UpdateCenter;

public sealed class UpdateCenterHandler(IFacilityRepository facilities, IUnitOfWork unitOfWork, TimeProvider clock)
    : IRequestHandler<UpdateCenterCommand, CenterDto>
{
    public async Task<CenterDto> Handle(UpdateCenterCommand request, CancellationToken ct)
    {
        Center.Validate(request.Name, request.Address, request.TimeZoneId);
        var center = await facilities.CenterAsync(request.Id, ct)
            ?? throw new ClassRuleException("center_not_found", "Center not found.", 404);
        center.Name = request.Name.Trim();
        center.Address = request.Address.Trim();
        center.TimeZoneId = request.TimeZoneId;
        center.UpdatedAt = clock.GetUtcNow().UtcDateTime;
        await unitOfWork.SaveChangesAsync(ct);
        return CenterDto.From(center);
    }
}
