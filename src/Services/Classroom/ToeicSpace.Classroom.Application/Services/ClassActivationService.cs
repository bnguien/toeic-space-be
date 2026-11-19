using ToeicSpace.Classroom.Domain.Entities;
using ToeicSpace.Classroom.Domain.Exceptions;

namespace ToeicSpace.Classroom.Application.Services;

public sealed class ClassActivationService(TeacherEligibilityService teachers)
{
    public async Task EnsureCanActivateAsync(Class entity, CancellationToken ct)
    {
        Class.ValidateMetadata(entity.Code, entity.Name, entity.Description, entity.StartDate, entity.EndDate);
        if (entity.StartDate is null || entity.EndDate is null)
            throw new ClassRuleException("required_dates", "Activation requires start and end dates.");
        var current = entity.Instructors.SingleOrDefault(x => x.UnassignedAt == null)
                      ?? throw new ClassRuleException("teacher_required", "Activation requires a current teacher.");
        await teachers.EnsureEligibleAsync(current.TeacherId, ct);
    }
}
