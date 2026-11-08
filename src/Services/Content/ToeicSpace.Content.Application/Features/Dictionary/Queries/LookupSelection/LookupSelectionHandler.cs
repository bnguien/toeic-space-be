using System.Text.RegularExpressions;
using ToeicSpace.Content.Application.DTOs.Dictionary;
using ToeicSpace.Content.Application.Interfaces.Repositories;
using ToeicSpace.Content.Application.Interfaces.Services.Enrichment;
using ToeicSpace.Content.Application.Models.Enrichment;
using ToeicSpace.Content.Application.Services.Enrichment;

namespace ToeicSpace.Content.Application.Features.Dictionary.Queries.LookupSelection;

public sealed partial class LookupSelectionHandler(WordEnricher enricher, ITranslationProvider translator,
    ILearnerLibraryRepository library, EnrichmentSettings settings)
    : IRequestHandler<LookupSelectionQuery, SelectionLookupDto>
{
    public const string SentenceKind = "sentence";

    public async Task<SelectionLookupDto> Handle(LookupSelectionQuery request, CancellationToken ct)
    {
        var signedIn = request.LearnerId != Guid.Empty;
        var text = WhitespaceRuns().Replace(request.Text, " ").Trim();
        return InputNormalizer.Normalize(text).Kind == WordKind.Reject
            ? await TranslateAsync(text, signedIn, ct)
            : await EnrichAsync(text, request, signedIn, ct);
    }

    /// <summary>Word or short phrase: the vocabulary tiers (cache, catalog, dictionary, translation, AI when allowed).</summary>
    private async Task<SelectionLookupDto> EnrichAsync(string text, LookupSelectionQuery request, bool signedIn,
        CancellationToken ct)
    {
        var allowAi = signedIn && request.AllowAi;
        var deadline = TimeSpan.FromSeconds(allowAi ? settings.LookupAiDeadlineSeconds : settings.LookupDeadlineSeconds);
        var result = await enricher.EnrichAsync(new EnrichRequest(text, request.Context),
            new EnrichOptions(allowAi, Interactive: true, deadline), ct);
        var gloss = result.Gloss;

        Guid? savedWordId = null;
        if (signedIn)
        {
            // Same rule as the vocabulary lookup: the official word itself, else one of the learner's custom words.
            var saved = await library.FindSavedByHeadwordAsync(request.LearnerId, gloss.NormalizedText, ct);
            savedWordId = (saved.FirstOrDefault(x => result.OfficialWordId is { } official
                               ? x.WordId == official
                               : x.Word!.IsCustom)
                           ?? saved.FirstOrDefault(x => x.Word!.IsCustom))?.Id;
        }

        return new SelectionLookupDto(gloss.Text, gloss.Kind.Key(), !string.IsNullOrWhiteSpace(gloss.MeaningVi),
            gloss.MeaningVi, gloss.Phonetic, gloss.PartOfSpeech, gloss.Cefr, gloss.DefinitionEn, gloss.Example,
            gloss.ExampleVi, gloss.PhraseType, result.Complete, result.AiUsed, AiAllowed: signedIn, savedWordId,
            result.OfficialWordId);
    }

    /// <summary>Longer selection (sentence or paragraph): machine translation only, nothing to enrich or save.</summary>
    private async Task<SelectionLookupDto> TranslateAsync(string text, bool signedIn, CancellationToken ct)
    {
        var meaning = (await translator.TranslateAsync(text, ct))?.Text ?? string.Empty;
        var found = meaning.Length > 0;
        return new SelectionLookupDto(text, SentenceKind, found, meaning, Phonetic: string.Empty,
            PartOfSpeech: null, Cefr: string.Empty, DefinitionEn: string.Empty, Example: string.Empty,
            ExampleVi: string.Empty, PhraseType: null, Complete: found, AiUsed: false, AiAllowed: signedIn,
            SavedWordId: null, OfficialWordId: null);
    }

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRuns();
}
