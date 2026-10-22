using Microsoft.Extensions.Logging;
using ToeicSpace.Classroom.Application.DTOs.Classes;
using ToeicSpace.Classroom.Application.Interfaces.Repositories;
using ToeicSpace.Classroom.Application.Interfaces;
using ToeicSpace.Classroom.Application.Mappings;
using ToeicSpace.Classroom.Domain.Exceptions;

namespace ToeicSpace.Classroom.Application.Features.Classes.Commands.UnassignTeacher;

public sealed class UnassignTeacherHandler(
    IClassRepository db,
    IUnitOfWork unitOfWork,
    TimeProvider clock,
    ILogger<UnassignTeacherHandler> logger) : IRequestHandler<UnassignTeacherCommand, ClassDetailDto>
{
    public async Task<ClassDetailDto> Handle(UnassignTeacherCommand request, CancellationToken ct)
    {
        var id = request.ClassId;
        var adminId = request.AdminId;

        await using var tx = await unitOfWork.BeginTransactionAsync(ct);
        var entity = await db.GetByIdAsync(id, false, ct) ?? throw ClassRuleException.NotFound();
        if (!entity.CanUnassignTeacher()) throw ClassRuleException.InvalidState();
        var current = entity.Instructors.SingleOrDefault(x => x.UnassignedAt == null)
                      ?? throw new ClassRuleException("teacher_required", "Class has no current teacher.", 409);
        current.UnassignedAt = clock.GetUtcNow().UtcDateTime;
        current.UnassignedBy = adminId;
        entity.Touch(adminId, clock.GetUtcNow().UtcDateTime);
        await unitOfWork.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        logger.LogInformation("Teacher {TeacherId} unassigned from class {ClassId} by {AdminId}", current.TeacherId, id,
            adminId);
        return entity.Detail();
    }
}