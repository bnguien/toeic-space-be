using ToeicSpace.Content.Application.Common.Pagination;
using ToeicSpace.Content.Application.DTOs.Vocabulary;
using ToeicSpace.Content.Application.Interfaces.Repositories;
using ToeicSpace.Content.Application.Mappings;
using ToeicSpace.Content.Application.Services;
using ToeicSpace.Content.Domain.Rules;

namespace ToeicSpace.Content.Application.Features.VocabularyProgress.Queries.GetMistakes;

public sealed class GetMistakesHandler(ILearnerProgressRepository progress, IVocabularyCatalogRepository catalog,
    PersonalWordOverlay overlay, TimeProvider clock) : IRequestHandler<GetMistakesQuery, PagedResult<MistakeItemDto>>
{
    public async Task<PagedResult<MistakeItemDto>> Handle(GetMistakesQuery request, CancellationToken ct)
    {
        var today = SpacedRepetitionScheduler.LocalDay(clock.GetUtcNow().UtcDateTime, request.TzOffset);
        var page = await progress.GetMistakesAsync(request.LearnerId, request.Page, request.PageSize, ct);
        var ids = page.Items.Select(x => x.WordId).ToArray();
        var words = (await catalog.GetWordsAsync(ids, request.LearnerId, ct)).ToDictionary(x => x.Id);
        var saved = await overlay.LoadAsync(request.LearnerId, ids, ct);
        return new PagedResult<MistakeItemDto>(page.Items.Where(x => words.ContainsKey(x.WordId))
            .Select(x => x.ToMistakeDto(words[x.WordId], saved.GetValueOrDefault(x.WordId), today)).ToArray(),
            page.Page, page.PageSize, page.TotalCount);
    }
}
