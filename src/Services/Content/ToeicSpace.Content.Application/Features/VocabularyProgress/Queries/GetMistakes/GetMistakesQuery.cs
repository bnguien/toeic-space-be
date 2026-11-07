using ToeicSpace.Content.Application.Common.Pagination;
using ToeicSpace.Content.Application.DTOs.Vocabulary;

namespace ToeicSpace.Content.Application.Features.VocabularyProgress.Queries.GetMistakes;

/// <summary>Pending mistakes ("Từ sai") of the learner, most recent mistake first.</summary>
public sealed record GetMistakesQuery(Guid LearnerId, int Page = 1, int PageSize = 20, int TzOffset = 0)
    : IRequest<PagedResult<MistakeItemDto>>
{
    public const int MaxPageSize = 100;
}
