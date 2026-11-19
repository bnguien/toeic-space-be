using ToeicSpace.Content.Application.DTOs.Vocabulary;
using ToeicSpace.Content.Application.Interfaces.Repositories;
using ToeicSpace.Content.Application.Mappings;
using ToeicSpace.Content.Application.Services;
using ToeicSpace.Content.Domain.Exceptions;

namespace ToeicSpace.Content.Application.Features.VocabularyCatalog.Queries.GetSetDetail;

/// <summary>A signed-in learner sees the words with their personal edits and notes.</summary>
public sealed class GetSetDetailHandler(IVocabularyCatalogRepository catalog, PersonalWordOverlay overlay)
    : IRequestHandler<GetSetDetailQuery, SetDetailDto>
{
    public async Task<SetDetailDto> Handle(GetSetDetailQuery request, CancellationToken ct)
    {
        var set = await catalog.GetSetDetailAsync(request.SetId, ct) ?? throw VocabularyErrors.SetNotFound();
        // Previous/next follow the whole roadmap, so the last lesson of a stage links to the next stage.
        var ordered = await catalog.GetOrderedSetIdsAsync(ct);
        var index = ordered.ToList().IndexOf(set.Id);
        Guid? previous = index > 0 ? ordered[index - 1] : null;
        Guid? next = index >= 0 && index < ordered.Count - 1 ? ordered[index + 1] : null;
        var detail = set.ToDetailDto(previous, next);
        return detail with { Words = await overlay.ApplyAsync(request.LearnerId, detail.Words, ct) };
    }
}
