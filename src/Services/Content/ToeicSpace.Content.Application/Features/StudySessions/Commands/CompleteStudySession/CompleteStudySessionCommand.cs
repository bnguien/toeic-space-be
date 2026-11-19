using ToeicSpace.Content.Application.DTOs.StudySessions;

namespace ToeicSpace.Content.Application.Features.StudySessions.Commands.CompleteStudySession;

/// <summary>Ends the session and returns its summary; completing again returns the same result.</summary>
public sealed record CompleteStudySessionCommand(Guid SessionId, Guid LearnerId, int TzOffset = 0)
    : IRequest<StudySessionDto>;
