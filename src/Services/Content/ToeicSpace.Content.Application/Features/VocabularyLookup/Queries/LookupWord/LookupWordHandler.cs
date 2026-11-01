using ToeicSpace.Content.Application.DTOs.VocabularyLookup;
using ToeicSpace.Content.Application.Interfaces.Repositories;
using ToeicSpace.Content.Application.Mappings;
using ToeicSpace.Content.Application.Models.Enrichment;
using ToeicSpace.Content.Application.Services.Enrichment;

namespace ToeicSpace.Content.Application.Features.VocabularyLookup.Queries.LookupWord;

public sealed class LookupWordHandler(WordEnricher enricher, ILearnerLibraryRepository library,
    EnrichmentSettings settings) : IRequestHandler<LookupWordQuery, WordLookupDto>
{
    public async Task<WordLookupDto> Handle(LookupWordQuery request, CancellationToken ct)
    {
        var deadline = TimeSpan.FromSeconds(request.AllowAi ? settings.LookupAiDeadlineSeconds : settings.LookupDeadlineSeconds);
        var result = await enricher.EnrichAsync(new EnrichRequest(request.Text, request.Context),
            new EnrichOptions(request.AllowAi, Interactive: true, deadline), ct);

        // Already saved: the official word itself, or one of the learner's own custom words with this headword.
        var saved = await library.FindSavedByHeadwordAsync(request.LearnerId, result.Gloss.NormalizedText, ct);
        var existing = saved.FirstOrDefault(x => result.OfficialWordId is { } official ? x.WordId == official : x.Word!.IsCustom)
            ?? saved.FirstOrDefault(x => x.Word!.IsCustom);
        return result.ToLookupDto(existing is null ? null : new ExistingWordDto(existing.Id, existing.FolderId));
    }
}
