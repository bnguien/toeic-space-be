using Microsoft.Extensions.Logging;
using ToeicSpace.Classroom.Application.Interfaces.Repositories;
using ToeicSpace.Classroom.Application.Interfaces;
using ToeicSpace.Classroom.Domain.Enums;
using ToeicSpace.Classroom.Domain.Exceptions;

namespace ToeicSpace.Classroom.Application.Features.Classes.Commands.HardDeleteClass;

public sealed class HardDeleteClassHandler(
    IClassRepository db,
    IUnitOfWork unitOfWork,
    ILogger<HardDeleteClassHandler> logger) : IRequestHandler<HardDeleteClassCommand>
{
    public async Task Handle(HardDeleteClassCommand request, CancellationToken ct)
    {
        var id = request.ClassId;

        var entity = await db.GetByIdAsync(id, true, ct) ?? throw ClassRuleException.NotFound();
        if (entity.Status != ClassStatus.Deleted || entity.Instructors.Count != 0)
            throw new ClassRuleException("delete_blocked",
                "Class must be soft deleted and have no related instructor history.", 409);
        db.Remove(entity);
        // The database's restrictive foreign keys also protect dependents introduced later.
        await unitOfWork.SaveChangesAsync(ct);

        logger.LogWarning("Class {ClassId} permanently deleted", id);
    }
}
