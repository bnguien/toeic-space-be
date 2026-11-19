using ToeicSpace.Content.Application.DTOs.Vocabulary;

namespace ToeicSpace.Content.Application.Features.VocabularyProgress.Queries.GetStageSetProgress;

public sealed record GetStageSetProgressQuery(Guid LearnerId, Guid StageId, int TzOffset = 0)
    : IRequest<IReadOnlyList<SetProgressDto>>;
