using Microsoft.Extensions.Logging;
using ToeicSpace.Classroom.Application.DTOs.Enrollments;
using ToeicSpace.Classroom.Application.Interfaces;
using ToeicSpace.Classroom.Application.Interfaces.Repositories;
using ToeicSpace.Classroom.Application.Mappings;
using ToeicSpace.Classroom.Domain.Enums;
using ToeicSpace.Classroom.Domain.Exceptions;

namespace ToeicSpace.Classroom.Application.Features.Enrollments.Commands.WithdrawStudent;

public sealed class WithdrawStudentHandler(
    IClassRepository classes, IEnrollmentRepository enrollments, IStudentReadRepository students,
    IUnitOfWork unitOfWork, TimeProvider clock, ILogger<WithdrawStudentHandler> logger)
    : IRequestHandler<WithdrawStudentCommand, ClassEnrollmentDto>
{
    public async Task<ClassEnrollmentDto> Handle(WithdrawStudentCommand request, CancellationToken ct)
    {
        logger.LogDebug("Checking withdrawal of enrollment {EnrollmentId} in class {ClassId}", request.EnrollmentId, request.ClassId);
        var entity = await classes.GetByIdAsync(request.ClassId, false, ct);
        if (entity is null)
        {
            logger.LogWarning("Withdrawal rejected: class {ClassId} not found or soft deleted", request.ClassId);
            throw ClassRuleException.NotFound();
        }
        if (!entity.CanManageEnrollments())
        {
            logger.LogWarning("Withdrawal rejected: class {ClassId} is {ClassStatus}", entity.Id, entity.Status);
            throw ClassRuleException.InvalidState();
        }
        var enrollment = await enrollments.GetByIdAsync(entity.Id, request.EnrollmentId, ct);
        if (enrollment is null)
        {
            logger.LogWarning("Withdrawal rejected: enrollment {EnrollmentId} not found in class {ClassId}", request.EnrollmentId, entity.Id);
            throw new ClassRuleException("enrollment_not_found", "Enrollment not found.", 404);
        }
        if (!enrollment.IsCurrent)
        {
            logger.LogWarning("Withdrawal rejected: enrollment {EnrollmentId} is {EnrollmentStatus}", enrollment.Id, enrollment.Status);
            throw new ClassRuleException("enrollment_not_active", "Enrollment is not active.", 409);
        }

        var now = clock.GetUtcNow().UtcDateTime;
        enrollment.Status = EnrollmentStatus.Withdrawn;
        enrollment.WithdrawnAt = now;
        enrollment.WithdrawnBy = request.AdminId;
        entity.Touch(request.AdminId, now);
        await unitOfWork.SaveChangesAsync(ct);
        var student = await students.GetByIdAsync(enrollment.StudentId, ct);
        logger.LogInformation("Enrollment {EnrollmentId} withdrawn from class {ClassId} by {AdminId}", enrollment.Id, entity.Id, request.AdminId);
        return enrollment.ToDto(student?.Name, student?.Email);
    }

}
