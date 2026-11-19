using ToeicSpace.Content.Application.DTOs.Vocabulary;

namespace ToeicSpace.Content.Application.Features.VocabularyProgress.Queries.GetLearnerSummary;

public sealed record GetLearnerSummaryQuery(Guid LearnerId, int TzOffset = 0) : IRequest<LearnerSummaryDto>;
