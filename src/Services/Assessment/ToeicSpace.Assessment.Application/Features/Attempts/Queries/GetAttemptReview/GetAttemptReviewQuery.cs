using ToeicSpace.Assessment.Application.DTOs.Attempts;

namespace ToeicSpace.Assessment.Application.Features.Attempts.Queries.GetAttemptReview;

/// <summary>A completed attempt with everything revealed: answers, explanations, scripts and translations.</summary>
public sealed record GetAttemptReviewQuery(Guid AttemptId) : IRequest<AttemptDto>;
