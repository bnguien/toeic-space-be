using ToeicSpace.Content.Application.Common.Pagination;
using ToeicSpace.Content.Application.DTOs.VocabularyLibrary;
using ToeicSpace.Content.Application.Interfaces.Repositories;
using ToeicSpace.Content.Application.Mappings;
using ToeicSpace.Content.Application.Services;
using ToeicSpace.Content.Domain.Rules;

namespace ToeicSpace.Content.Application.Features.VocabularyLibrary.Queries.GetSavedWords;

public sealed class GetSavedWordsHandler(ILearnerLibraryRepository library, ILearnerProgressRepository progress,
    LibraryWordService words, TimeProvider clock) : IRequestHandler<GetSavedWordsQuery, PagedResult<SavedWordDto>>
{
    public async Task<PagedResult<SavedWordDto>> Handle(GetSavedWordsQuery request, CancellationToken ct)
    {
        await words.EnsureFolderAsync(request.LearnerId, request.FolderId, ct);
        var today = SpacedRepetitionScheduler.LocalDay(clock.GetUtcNow().UtcDateTime, request.TzOffset);
        var page = await library.SearchSavedWordsAsync(request.LearnerId, request.FolderId, request.Unfiled == true,
            string.IsNullOrWhiteSpace(request.Search) ? null : request.Search.Trim(), request.Page, request.PageSize, ct);
        var rows = (await progress.GetForWordsAsync(request.LearnerId, page.Items.Select(x => x.WordId).ToArray(), ct))
            .ToDictionary(x => x.WordId);
        return new PagedResult<SavedWordDto>(page.Items.Select(x => x.ToDto(rows.GetValueOrDefault(x.WordId), today))
            .ToArray(), page.Page, page.PageSize, page.TotalCount);
    }
}
