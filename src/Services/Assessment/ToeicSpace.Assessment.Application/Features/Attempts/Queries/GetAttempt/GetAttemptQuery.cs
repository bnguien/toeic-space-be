using ToeicSpace.Assessment.Application.DTOs.Attempts;

namespace ToeicSpace.Assessment.Application.Features.Attempts.Queries.GetAttempt;

/// <summary>The learner's own attempt (any status). Other users' attempts are reported as unavailable.</summary>
public sealed record GetAttemptQuery(Guid AttemptId) : IRequest<AttemptDto>;
