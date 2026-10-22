using ToeicSpace.Classroom.Application.Services;
using Microsoft.Extensions.Logging;
using ToeicSpace.Classroom.Application.DTOs.Classes;
using ToeicSpace.Classroom.Application.Interfaces.Repositories;
using ToeicSpace.Classroom.Application.Interfaces;
using ToeicSpace.Classroom.Application.Mappings;
using ToeicSpace.Classroom.Domain.Entities;
using ToeicSpace.Classroom.Domain.Exceptions;

namespace ToeicSpace.Classroom.Application.Features.Classes.Commands.AssignTeacher;

public sealed class AssignTeacherHandler(
    IClassRepository db,
    IUnitOfWork unitOfWork,
    TeacherEligibilityService eligibility,
    TimeProvider clock,
    ILogger<AssignTeacherHandler> logger) : IRequestHandler<AssignTeacherCommand, ClassDetailDto>
{
    public async Task<ClassDetailDto> Handle(AssignTeacherCommand request, CancellationToken ct)
    {
        var id = request.ClassId;
        var teacherId = request.TeacherId;
        var adminId = request.AdminId;

        if (teacherId == Guid.Empty) throw new ClassRuleException("teacher_required", "TeacherId is required.");
        // Validate the local projection before opening the transaction; failure cannot touch the old assignment.
        await eligibility.EnsureEligibleAsync(teacherId, ct);
        await using var tx = await unitOfWork.BeginTransactionAsync(ct);
        var entity = await db.GetByIdAsync(id, false, ct) ?? throw ClassRuleException.NotFound();
        if (!entity.CanAssignTeacher()) throw ClassRuleException.InvalidState();
        var current = entity.Instructors.SingleOrDefault(x => x.UnassignedAt == null);
        if (current?.TeacherId == teacherId)
            throw new ClassRuleException("already_assigned", "Teacher is already current.", 409);
        var now = clock.GetUtcNow().UtcDateTime;
        if (current is not null)
        {
            current.UnassignedAt = now;
            current.UnassignedBy = adminId;
        }

        entity.Touch(adminId, clock.GetUtcNow().UtcDateTime);
        // Release the current unique slot and claim the Class version before inserting the replacement.
        await unitOfWork.SaveChangesAsync(ct);
        db.Add(new ClassInstructor { ClassId = id, TeacherId = teacherId, AssignedAt = now, AssignedBy = adminId });
        await unitOfWork.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        logger.LogInformation(
            "Teacher {TeacherId} assigned to class {ClassId} by {AdminId}; previous teacher {PreviousTeacherId}",
            teacherId, id, adminId, current?.TeacherId);
        return entity.Detail();
    }
}
