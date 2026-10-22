using Microsoft.Extensions.Logging;
using ToeicSpace.Classroom.Application.DTOs.Classes;
using ToeicSpace.Classroom.Application.Interfaces.Repositories;
using ToeicSpace.Classroom.Application.Interfaces;
using ToeicSpace.Classroom.Application.Mappings;
using ToeicSpace.Classroom.Application.Services;
using ToeicSpace.Classroom.Domain.Enums;
using ToeicSpace.Classroom.Domain.Exceptions;

namespace ToeicSpace.Classroom.Application.Features.Classes.Commands.RestoreDeletedClass;

public sealed class RestoreDeletedClassHandler(IClassRepository db, IUnitOfWork unitOfWork, ClassRestorationService restoration, TimeProvider clock, ILogger<RestoreDeletedClassHandler> logger) : IRequestHandler<RestoreDeletedClassCommand, ClassDetailDto>
{
    public async Task<ClassDetailDto> Handle(RestoreDeletedClassCommand request, CancellationToken ct)
    {
        var id = request.ClassId; var adminId = request.AdminId;

        var entity = await db.GetByIdAsync(id, true, ct) ?? throw ClassRuleException.NotFound();
        if (entity.Status != ClassStatus.Deleted) throw ClassRuleException.InvalidState();
        var original = await restoration.GetOriginalStatusAsync(entity, ct);
        entity.Status = original;
        entity.ArchivedFromStatus = null;
        entity.Touch(adminId, clock.GetUtcNow().UtcDateTime);
        await unitOfWork.SaveChangesAsync(ct);
        logger.LogInformation("Class {ClassId} restored from soft delete to {Status} by {AdminId}", id, original, adminId);
        return entity.Detail();
    }
}
