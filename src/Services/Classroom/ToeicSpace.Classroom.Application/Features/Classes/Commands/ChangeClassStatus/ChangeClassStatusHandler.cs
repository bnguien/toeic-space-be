using ToeicSpace.Classroom.Application.Services;
using Microsoft.Extensions.Logging;
using ToeicSpace.Classroom.Application.DTOs.Classes;
using ToeicSpace.Classroom.Application.Interfaces.Repositories;
using ToeicSpace.Classroom.Application.Interfaces;
using ToeicSpace.Classroom.Application.Mappings;
using ToeicSpace.Classroom.Domain.Enums;
using ToeicSpace.Classroom.Domain.Exceptions;

namespace ToeicSpace.Classroom.Application.Features.Classes.Commands.ChangeClassStatus;

public sealed class ChangeClassStatusHandler(
    IClassRepository db,
    IUnitOfWork unitOfWork,
    ClassActivationService activation,
    TimeProvider clock,
    ILogger<ChangeClassStatusHandler> logger) : IRequestHandler<ChangeClassStatusCommand, ClassDetailDto>
{
    public async Task<ClassDetailDto> Handle(ChangeClassStatusCommand request, CancellationToken ct)
    {
        var id = request.ClassId;
        var target = request.Status;
        var adminId = request.AdminId;

        var entity = await db.GetByIdAsync(id, false, ct) ?? throw ClassRuleException.NotFound();
        if (target == ClassStatus.Deleted || entity.Status == ClassStatus.Deleted)
            throw new ClassRuleException("invalid_transition", "Use the dedicated delete or admin restore action.",
                409);
        if (!entity.CanTransitionTo(target))
            throw new ClassRuleException("invalid_transition", "Invalid class status transition.", 409);
        if (target == ClassStatus.Active)
            await activation.EnsureCanActivateAsync(entity, ct);

        var previousStatus = entity.Status;
        if (target == ClassStatus.Archived)
            entity.ArchivedFromStatus = previousStatus;
        entity.Status = target;
        entity.Touch(adminId, clock.GetUtcNow().UtcDateTime);
        await unitOfWork.SaveChangesAsync(ct);
        logger.LogInformation("Class {ClassId} status changed from {PreviousStatus} to {Status} by {AdminId}", id,
            previousStatus, target, adminId);
        return entity.Detail();
    }
}
