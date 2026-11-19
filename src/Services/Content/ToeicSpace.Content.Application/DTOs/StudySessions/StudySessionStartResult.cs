namespace ToeicSpace.Content.Application.DTOs.StudySessions;

/// <summary><paramref name="Created"/> is false when an active session was resumed.</summary>
public sealed record StudySessionStartResult(StudySessionDto Session, bool Created);
