using ToeicSpace.Classroom.Domain.Enums;
using ToeicSpace.Classroom.Domain.Exceptions;

namespace ToeicSpace.Classroom.Domain.Entities;

public sealed class Class
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public ClassStatus Status { get; set; } = ClassStatus.Draft;
    public ClassStatus? ArchivedFromStatus { get; set; }
    public DateOnly? StartDate { get; set; }
    public DateOnly? EndDate { get; set; }
    public DateTime CreatedAt { get; set; }
    public Guid CreatedBy { get; set; }
    public DateTime UpdatedAt { get; set; }
    public Guid UpdatedBy { get; set; }
    public int Version { get; set; }
    public List<ClassInstructor> Instructors { get; set; } = [];
    public List<ClassEnrollment> Enrollments { get; set; } = [];

    public void Touch(Guid adminId, DateTime now)
    {
        UpdatedAt = now;
        UpdatedBy = adminId;
        Version++;
    }

    public static void ValidateMetadata(string code, string name, string? description, DateOnly? start, DateOnly? end)
    {
        if (string.IsNullOrWhiteSpace(code) || code.Length > 50 || string.IsNullOrWhiteSpace(name) || name.Length > 200)
            throw new ClassRuleException("invalid_class", "Code and name are required and must fit their length limits.");
        if (description?.Length > 2000)
            throw new ClassRuleException("invalid_description", "Description must be at most 2000 characters.");
        if (start is not null && end is not null && start > end)
            throw new ClassRuleException("invalid_date_range", "StartDate cannot exceed EndDate.");
    }

    public bool CanTransitionTo(ClassStatus target) => (Status, target) switch
    {
        (ClassStatus.Draft, ClassStatus.Active or ClassStatus.Archived) => true,
        (ClassStatus.Active, ClassStatus.Completed or ClassStatus.Archived) => true,
        (ClassStatus.Completed, ClassStatus.Archived) => true,
        _ => false
    };

    public bool CanEdit() => Status is ClassStatus.Draft or ClassStatus.Active;

    public bool CanAssignTeacher() => Status is ClassStatus.Draft or ClassStatus.Active;

    public bool CanManageEnrollments() => Status is ClassStatus.Draft or ClassStatus.Active;

    public bool CanUnassignTeacher() => Status == ClassStatus.Draft;
}
