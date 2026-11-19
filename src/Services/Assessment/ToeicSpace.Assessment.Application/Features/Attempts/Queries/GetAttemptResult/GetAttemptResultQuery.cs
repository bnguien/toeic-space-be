using ToeicSpace.Assessment.Application.DTOs.Attempts;

namespace ToeicSpace.Assessment.Application.Features.Attempts.Queries.GetAttemptResult;

public sealed record GetAttemptResultQuery(Guid AttemptId) : IRequest<AttemptResultDto>;
