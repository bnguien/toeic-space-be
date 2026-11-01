using ToeicSpace.Content.Application.DTOs.StudySessions;

namespace ToeicSpace.Content.Application.Features.StudySessions.Queries.GetStudySession;

public sealed record GetStudySessionQuery(Guid LearnerId, Guid SessionId, int TzOffset = 0) : IRequest<StudySessionDto>;
