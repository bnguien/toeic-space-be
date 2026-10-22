using Microsoft.Extensions.Logging;
using ToeicSpace.Classroom.Application.Interfaces.Repositories;
using ToeicSpace.Classroom.Application.Interfaces;
using ToeicSpace.Classroom.Domain.Enums;
using ToeicSpace.Classroom.Domain.Exceptions;

namespace ToeicSpace.Classroom.Application.Features.Classes.Commands.DeleteClass;

public sealed class DeleteClassHandler(
    IClassRepository db,
    IUnitOfWork unitOfWork,
    TimeProvider clock,
    ILogger<DeleteClassHandler> logger) : IRequestHandler<DeleteClassCommand>
{
    public async Task Handle(DeleteClassCommand request, CancellationToken ct)
    {
        var id = request.ClassId;
        var adminId = request.AdminId;

        var entity = await db.GetByIdAsync(id, false, ct) ?? throw ClassRuleException.NotFound();
        if (entity.Status != ClassStatus.Archived) throw ClassRuleException.InvalidState();
        entity.Status = ClassStatus.Deleted;
        entity.Touch(adminId, clock.GetUtcNow().UtcDateTime);
        await unitOfWork.SaveChangesAsync(ct);
        logger.LogInformation("Class {ClassId} soft deleted by {AdminId}", id, adminId);
    }
}