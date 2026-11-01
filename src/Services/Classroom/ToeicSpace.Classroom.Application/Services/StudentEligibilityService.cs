using Microsoft.Extensions.Logging;
using ToeicSpace.Classroom.Application.Interfaces.Repositories;
using ToeicSpace.Classroom.Domain.Entities;
using ToeicSpace.Classroom.Domain.Exceptions;

namespace ToeicSpace.Classroom.Application.Services;

public sealed class StudentEligibilityService(IStudentReadRepository students, ILogger<StudentEligibilityService> logger)
{
    public async Task<StudentReadModel> EnsureEligibleAsync(Guid studentId, CancellationToken ct)
    {
        var student = await students.GetByIdAsync(studentId, ct);
        if (student is null)
        {
            logger.LogWarning("Student {StudentId} is missing from the local Identity projection", studentId);
            throw new ClassRuleException("student_not_synchronized", "Student data is not synchronized yet. Retry later.", 503);
        }
        if (!string.Equals(student.Role, "User", StringComparison.OrdinalIgnoreCase))
        {
            logger.LogWarning("Enrollment eligibility rejected for {StudentId}: role {Role}", studentId, student.Role);
            throw new ClassRuleException("not_student", "Selected user is not a student.");
        }
        if (!student.IsVerified)
        {
            logger.LogWarning("Enrollment eligibility rejected for {StudentId}: account is unverified", studentId);
            throw new ClassRuleException("unverified_student", "Selected student is not verified.");
        }
        if (!string.Equals(student.Status, "Active", StringComparison.OrdinalIgnoreCase) || !student.IsEligible)
        {
            logger.LogWarning("Enrollment eligibility rejected for {StudentId}: status {Status}, eligible {IsEligible}",
                studentId, student.Status, student.IsEligible);
            throw new ClassRuleException("inactive_student", "Selected student is not active.");
        }
        return student;
    }
}
