namespace ToeicSpace.Classroom.Domain.Entities;

public sealed class StudentReadModel
{
    public Guid StudentId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public bool IsVerified { get; set; }
    public bool IsEligible { get; set; }
    public long Version { get; set; }
    public Guid LastEventId { get; set; }
    public DateTime SynchronizedAt { get; set; }
}
