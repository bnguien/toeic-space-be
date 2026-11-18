namespace ToeicSpace.Classroom.Domain.Entities;

public sealed class ClassScheduleAudit
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ClassScheduleId { get; set; }
    public Guid ActorId { get; set; }
    public DateTime ChangedAt { get; set; }
    public string Action { get; set; } = string.Empty;
    public string? PreviousValue { get; set; }
    public string NewValue { get; set; } = string.Empty;
}
