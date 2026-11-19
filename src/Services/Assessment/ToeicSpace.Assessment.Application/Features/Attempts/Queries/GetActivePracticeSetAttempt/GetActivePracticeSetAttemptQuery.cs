using ToeicSpace.Assessment.Application.DTOs.Attempts;

namespace ToeicSpace.Assessment.Application.Features.Attempts.Queries.GetActivePracticeSetAttempt;

public sealed record GetActivePracticeSetAttemptQuery(Guid PracticeSetId) : IRequest<ActivePracticeAttemptDto>;
