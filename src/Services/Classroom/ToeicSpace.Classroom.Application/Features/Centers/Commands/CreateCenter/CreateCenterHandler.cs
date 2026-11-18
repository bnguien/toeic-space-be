using ToeicSpace.Classroom.Application.DTOs.Facilities;
using ToeicSpace.Classroom.Application.Interfaces;
using ToeicSpace.Classroom.Application.Interfaces.Repositories;
using ToeicSpace.Classroom.Domain.Entities;

namespace ToeicSpace.Classroom.Application.Features.Centers.Commands.CreateCenter;

public sealed class CreateCenterHandler(IFacilityRepository facilities, IUnitOfWork unitOfWork, TimeProvider clock)
    : IRequestHandler<CreateCenterCommand, CenterDto>
{
    public async Task<CenterDto> Handle(CreateCenterCommand request, CancellationToken ct)
    {
        var zone = request.TimeZoneId ?? "Asia/Ho_Chi_Minh";
        Center.Validate(request.Name, request.Address, zone);
        var now = clock.GetUtcNow().UtcDateTime;
        var center = new Center { Name = request.Name.Trim(), Address = request.Address.Trim(),
            TimeZoneId = zone, CreatedAt = now, UpdatedAt = now };
        facilities.Add(center);
        await unitOfWork.SaveChangesAsync(ct);
        return CenterDto.From(center);
    }
}
