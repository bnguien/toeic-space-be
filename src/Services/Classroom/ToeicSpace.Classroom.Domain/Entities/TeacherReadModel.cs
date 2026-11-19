namespace ToeicSpace.Classroom.Domain.Entities;

public sealed class TeacherReadModel
{
    public Guid TeacherId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? AvatarUrl { get; set; }
    public string Role { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public bool IsEligible { get; set; }
    public long Version { get; set; }
    public Guid LastEventId { get; set; }
    public DateTime SynchronizedAt { get; set; }
}
