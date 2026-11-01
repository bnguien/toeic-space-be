using Microsoft.Extensions.Logging;
using ToeicSpace.Classroom.Application.Interfaces.Repositories;
using ToeicSpace.Classroom.Application.Interfaces;
using ToeicSpace.Classroom.Domain.Enums;
using ToeicSpace.Classroom.Domain.Exceptions;

namespace ToeicSpace.Classroom.Application.Features.Classes.Commands.HardDeleteClass;

public sealed class HardDeleteClassHandler(
    IClassRepository db,
    IEnrollmentRepository enrollments,
    IUnitOfWork unitOfWork,
    ILogger<HardDeleteClassHandler> logger) : IRequestHandler<HardDeleteClassCommand>
{
    public async Task Handle(HardDeleteClassCommand request, CancellationToken ct)
    {
        var id = request.ClassId;

        var entity = await db.GetByIdAsync(id, true, ct) ?? throw ClassRuleException.NotFound();
        var hasEnrollmentHistory = await enrollments.HasHistoryAsync(id, ct);
        if (entity.Status != ClassStatus.Deleted || entity.Instructors.Count != 0
            || hasEnrollmentHistory)
        {
            logger.LogWarning("Permanent deletion blocked for class {ClassId}: status {Status}, instructor history {InstructorCount}, enrollment history {HasEnrollmentHistory}",
                id, entity.Status, entity.Instructors.Count, hasEnrollmentHistory);
            throw new ClassRuleException("delete_blocked",
                "Class must be soft deleted and have no related instructor or enrollment history.", 409);
        }
        db.Remove(entity);
        // The database's restrictive foreign keys also protect dependents introduced later.
        await unitOfWork.SaveChangesAsync(ct);

        logger.LogWarning("Class {ClassId} permanently deleted", id);
    }
}
