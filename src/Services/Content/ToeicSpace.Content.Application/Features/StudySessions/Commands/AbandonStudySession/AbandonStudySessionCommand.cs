namespace ToeicSpace.Content.Application.Features.StudySessions.Commands.AbandonStudySession;

/// <summary>Abandons an active session; already finished sessions are left as they are.</summary>
public sealed record AbandonStudySessionCommand(Guid SessionId, Guid LearnerId) : IRequest;
