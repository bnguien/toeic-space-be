namespace ToeicSpace.Classroom.Domain.Entities;

public sealed class ClassSessionAudit
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ClassSessionId { get; set; }
    public Guid ActorId { get; set; }
    public DateTime ChangedAt { get; set; }
    public string Action { get; set; } = string.Empty;
    public string? Reason { get; set; }
    public string? PreviousValue { get; set; }
    public string? NewValue { get; set; }
}
