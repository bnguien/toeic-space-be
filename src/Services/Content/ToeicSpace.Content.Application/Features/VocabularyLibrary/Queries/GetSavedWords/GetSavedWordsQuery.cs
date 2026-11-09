using ToeicSpace.Content.Application.Common.Pagination;
using ToeicSpace.Content.Application.DTOs.VocabularyLibrary;

namespace ToeicSpace.Content.Application.Features.VocabularyLibrary.Queries.GetSavedWords;

/// <summary><see cref="FolderId"/>, <see cref="Unfiled"/> = true, or neither (= whole library); newest first.</summary>
public sealed record GetSavedWordsQuery(Guid LearnerId, Guid? FolderId, bool? Unfiled, string? Search,
    int Page = 1, int PageSize = 20, int TzOffset = 0) : IRequest<PagedResult<SavedWordDto>>
{
    public const int MaxPageSize = 100;
}
