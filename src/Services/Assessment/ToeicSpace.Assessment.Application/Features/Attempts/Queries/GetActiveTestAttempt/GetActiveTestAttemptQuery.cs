using ToeicSpace.Assessment.Application.DTOs.Attempts;

namespace ToeicSpace.Assessment.Application.Features.Attempts.Queries.GetActiveTestAttempt;

public sealed record GetActiveTestAttemptQuery(Guid TestId) : IRequest<ActiveTestAttemptDto>;
