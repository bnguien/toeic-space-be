using Microsoft.Extensions.Logging;
using ToeicSpace.Classroom.Application.DTOs.Enrollments;
using ToeicSpace.Classroom.Application.Interfaces;
using ToeicSpace.Classroom.Application.Interfaces.Repositories;
using ToeicSpace.Classroom.Application.Mappings;
using ToeicSpace.Classroom.Application.Services;
using ToeicSpace.Classroom.Domain.Entities;
using ToeicSpace.Classroom.Domain.Exceptions;

namespace ToeicSpace.Classroom.Application.Features.Enrollments.Commands.EnrollStudent;

public sealed class EnrollStudentHandler(
    IClassRepository classes, IEnrollmentRepository enrollments, StudentEligibilityService students,
    IUnitOfWork unitOfWork, TimeProvider clock, ILogger<EnrollStudentHandler> logger)
    : IRequestHandler<EnrollStudentCommand, ClassEnrollmentDto>
{
    public async Task<ClassEnrollmentDto> Handle(EnrollStudentCommand request, CancellationToken ct)
    {
        logger.LogDebug("Checking enrollment for student {StudentId} in class {ClassId}", request.StudentId, request.ClassId);
        var entity = await classes.GetByIdAsync(request.ClassId, false, ct);
        if (entity is null)
        {
            logger.LogWarning("Enrollment rejected: class {ClassId} not found or soft deleted", request.ClassId);
            throw ClassRuleException.NotFound();
        }
        if (!entity.CanManageEnrollments())
        {
            logger.LogWarning("Enrollment rejected: class {ClassId} is {ClassStatus}", entity.Id, entity.Status);
            throw ClassRuleException.InvalidState();
        }
        var student = await students.EnsureEligibleAsync(request.StudentId, ct);
        if (await enrollments.HasActiveAsync(entity.Id, request.StudentId, ct))
        {
            logger.LogWarning("Enrollment rejected: student {StudentId} is already active in class {ClassId}", request.StudentId, entity.Id);
            throw new ClassRuleException("already_enrolled", "Student is already enrolled in this class.", 409);
        }

        var now = clock.GetUtcNow().UtcDateTime;
        var enrollment = new ClassEnrollment
        {
            ClassId = entity.Id, StudentId = request.StudentId, EnrolledAt = now, EnrolledBy = request.AdminId
        };
        // Claim the class concurrency token so archive/delete cannot race this enrollment.
        entity.Touch(request.AdminId, now);
        enrollments.Add(enrollment);
        await unitOfWork.SaveChangesAsync(ct);
        logger.LogInformation("Enrollment {EnrollmentId} created for student {StudentId} in class {ClassId} by {AdminId}",
            enrollment.Id, request.StudentId, entity.Id, request.AdminId);
        return enrollment.ToDto(student.Name, student.Email);
    }
}
