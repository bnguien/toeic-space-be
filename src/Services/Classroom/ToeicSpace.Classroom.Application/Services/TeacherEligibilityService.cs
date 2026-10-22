using ToeicSpace.Classroom.Application.Interfaces.Repositories;
using ToeicSpace.Classroom.Domain.Exceptions;

namespace ToeicSpace.Classroom.Application.Services;

public sealed class TeacherEligibilityService(ITeacherReadRepository teachers)
{
    public async Task EnsureEligibleAsync(Guid teacherId, CancellationToken ct)
    {
        var teacher = await teachers.GetByIdAsync(teacherId, ct);
        if (teacher is null)
            throw new ClassRuleException("teacher_not_synchronized", "Teacher data is not synchronized yet. Retry later.", 503);
        if (!teacher.IsEligible)
        {
            var code = !string.Equals(teacher.Role, "Teacher", StringComparison.OrdinalIgnoreCase)
                ? "notteacher"
                : "inactive";
            throw new ClassRuleException(code, "Selected user is not an active teacher.");
        }
    }
}
