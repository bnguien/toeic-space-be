using Microsoft.Extensions.Logging;
using ToeicSpace.Classroom.Application.DTOs.Classes;
using ToeicSpace.Classroom.Application.Interfaces;
using ToeicSpace.Classroom.Application.Interfaces.Repositories;
using ToeicSpace.Classroom.Application.Mappings;
using ToeicSpace.Classroom.Application.Services;
using ToeicSpace.Classroom.Domain.Enums;
using ToeicSpace.Classroom.Domain.Exceptions;

namespace ToeicSpace.Classroom.Application.Features.Classes.Commands.RestoreArchivedClass;

public sealed class RestoreArchivedClassHandler(
    IClassRepository classes,
    IUnitOfWork unitOfWork,
    ClassRestorationService restoration,
    TimeProvider clock,
    ILogger<RestoreArchivedClassHandler> logger) : IRequestHandler<RestoreArchivedClassCommand, ClassDetailDto>
{
    public async Task<ClassDetailDto> Handle(RestoreArchivedClassCommand request, CancellationToken ct)
    {
        var entity = await classes.GetByIdAsync(request.ClassId, false, ct) ?? throw ClassRuleException.NotFound();
        if (entity.Status != ClassStatus.Archived)
            throw ClassRuleException.InvalidState();
        var previousStatus = await restoration.GetOriginalStatusAsync(entity, ct);

        entity.Status = previousStatus;
        entity.ArchivedFromStatus = null;
        entity.Touch(request.AdminId, clock.GetUtcNow().UtcDateTime);
        await unitOfWork.SaveChangesAsync(ct);
        logger.LogInformation("Class {ClassId} restored from Archived to {Status} by {AdminId}", entity.Id, previousStatus, request.AdminId);
        return entity.Detail();
    }
}
