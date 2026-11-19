namespace ToeicSpace.Classroom.Domain.Entities;

public sealed class TeacherGoogleConnection
{
    public Guid TeacherId { get; set; }
    public Guid ConnectionId { get; set; } = Guid.NewGuid();
    public string Email { get; set; } = string.Empty;
    public string EncryptedRefreshToken { get; set; } = string.Empty;
    public DateTime ConnectedAt { get; set; }
    public DateTime? RevokedAt { get; set; }
    public int Version { get; set; }
}

public sealed class GoogleOAuthAttempt
{
    public string StateHash { get; set; } = string.Empty;
    public Guid TeacherId { get; set; }
    public DateTime ExpiresAt { get; set; }
}

public sealed class SessionMeetProvisioning
{
    public Guid ClassSessionId { get; set; }
    public Guid TeacherId { get; set; }
    public Guid? ConnectionId { get; set; }
    public int SessionVersion { get; set; }
    public string Status { get; set; } = "Pending";
    public string? SpaceName { get; set; }
    public string? MeetingUrl { get; set; }
    public string? ErrorCode { get; set; }
    public DateTime? NextAttemptAt { get; set; }
    public DateTime? LeaseUntil { get; set; }
    public Guid? LeaseId { get; set; }
    public int Attempts { get; set; }
    public DateTime UpdatedAt { get; set; }
    public int Version { get; set; }
}
