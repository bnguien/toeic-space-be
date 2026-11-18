using ToeicSpace.Classroom.Domain.Enums;

namespace ToeicSpace.Classroom.Domain.Entities;

public sealed class ClassSession
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ClassId { get; set; }
    public Guid? ClassScheduleId { get; set; }
    public DateOnly? OriginalLocalDate { get; set; }
    public Guid TeacherId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public SessionType SessionType { get; set; } = SessionType.Regular;
    public DateTime StartAt { get; set; }
    public DateTime EndAt { get; set; }
    public SessionMode Mode { get; set; }
    public Guid? RoomId { get; set; }
    public SessionStatus Status { get; set; } = SessionStatus.Scheduled;
    public string? Notes { get; set; }
    public string? CancellationReason { get; set; }
    public DateTime? CompletedAt { get; set; }
    public bool TimeOverridden { get; set; }
    public bool RoomOverridden { get; set; }
    public bool TeacherOverridden { get; set; }
    public bool ModeOverridden { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public int Version { get; set; }
}
