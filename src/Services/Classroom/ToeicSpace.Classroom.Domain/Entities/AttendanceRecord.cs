using ToeicSpace.Classroom.Domain.Enums;

namespace ToeicSpace.Classroom.Domain.Entities;

public sealed class AttendanceRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ClassSessionId { get; set; }
    public Guid StudentId { get; set; }
    public AttendanceStatus Status { get; set; }
    public string? Note { get; set; }
    public Guid RecordedBy { get; set; }
    public DateTime RecordedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public sealed class AttendanceAudit
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ClassSessionId { get; set; }
    public Guid StudentId { get; set; }
    public AttendanceStatus? PreviousStatus { get; set; }
    public AttendanceStatus? NewStatus { get; set; }
    public string Action { get; set; } = "";
    public Guid ModifiedBy { get; set; }
    public DateTime ModifiedAt { get; set; }
    public string? Note { get; set; }
}