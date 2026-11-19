using ToeicSpace.Content.Application.DTOs.Videos;
using ToeicSpace.Content.Application.Interfaces.Repositories;
using ToeicSpace.Content.Application.Interfaces.Services.Video;
using ToeicSpace.Content.Application.Mappings;
using ToeicSpace.Content.Application.Models.Videos;
using ToeicSpace.Content.Domain.Rules.Videos;

namespace ToeicSpace.Content.Application.Services.Video;

/// <summary>
/// Word card details (Studychill <c>word_detail.lookup_detail</c>): dictionary synonyms/antonyms/senses/US IPA plus the
/// official words' synonyms and word family (same stem, ≤ 4), cached 14 days (10 minutes when the dictionary failed);
/// the basic gloss comes from the free enrichment tiers.
/// </summary>
public sealed class WordDetailService(IWordDetailDictionary dictionary, IVideoLexiconRepository lexicon,
    VideoGlossService gloss, IVideoCache cache)
{
    public const int MaxSynonyms = 6;
    public const int MaxFamily = 4;
    public const int MaxMeaningWords = 20;
    public static readonly TimeSpan CacheTtl = TimeSpan.FromDays(14);
    public static readonly TimeSpan FailureTtl = TimeSpan.FromMinutes(10);
    public static readonly TimeSpan BaseDeadline = TimeSpan.FromSeconds(6);

    private sealed record CachedDetail(string Ipa, string PartOfSpeech, IReadOnlyList<string> Synonyms,
        IReadOnlyList<string> Antonyms, IReadOnlyList<WordDefinitionDto> Definitions, IReadOnlyList<WordFamilyDto> Family);

    public async Task<WordInfoDto?> LookupAsync(string? rawWord, CancellationToken ct)
    {
        var word = WordDetailRules.NormalizeWord(rawWord);
        if (word.Length == 0) return null;
        var key = $"video:word-detail:v1:{word}";
        if (!cache.TryGet<CachedDetail>(key, out var detail) || detail is null)
        {
            var fromDictionary = await dictionary.LookupAsync(word, ct);
            var synonyms = new List<string>();
            foreach (var s in await lexicon.GetOfficialSynonymsAsync(word, ct)) WordDetailRules.AddUnique(synonyms, s, word);
            foreach (var s in fromDictionary?.Synonyms ?? []) WordDetailRules.AddUnique(synonyms, s, word);
            var family = new List<WordFamilyDto>();
            var stem = WordDetailRules.Stem(word);
            if (stem.Length >= 5)
                foreach (var entry in await lexicon.GetOfficialFamilyAsync(stem, word, 20, ct))
                {
                    var headword = entry.Headword.Trim();
                    if (family.Count >= MaxFamily || headword.Contains(' ')
                        || family.Any(f => string.Equals(f.Word, headword, StringComparison.OrdinalIgnoreCase))) continue;
                    var meaning = entry.MeaningVi.Split(',')[0].Trim();
                    family.Add(new WordFamilyDto(headword, meaning.Length > 60 ? meaning[..60] : meaning,
                        VideoMappingExtensions.PosKey(entry.PartOfSpeech)));
                }
            detail = new CachedDetail(fromDictionary?.Ipa ?? string.Empty, fromDictionary?.PartOfSpeech ?? string.Empty,
                synonyms.Take(MaxSynonyms).ToList(), (fromDictionary?.Antonyms ?? []).Take(MaxSynonyms).ToList(),
                (fromDictionary?.Senses ?? []).Select(x => new WordDefinitionDto(x.PartOfSpeech, x.Definition, x.Example)).ToList(),
                family);
            cache.Set(key, detail, fromDictionary is null ? FailureTtl : CacheTtl);
        }
        var basic = await gloss.EnrichAsync([word], allowAi: false, BaseDeadline, ct);
        return new WordInfoDto(word, detail.Ipa, detail.PartOfSpeech, detail.Synonyms, detail.Antonyms, detail.Definitions,
            detail.Family, basic.TryGetValue(word, out var entryBase) ? entryBase : GlossEntryDto.Empty);
    }

    /// <summary>Basic meanings (no AI) for up to 20 words — guests on the lesson summary.</summary>
    public async Task<IReadOnlyDictionary<string, GlossEntryDto>> QuickMeaningsAsync(IEnumerable<string> rawWords,
        CancellationToken ct)
    {
        var words = rawWords.Select(WordDetailRules.NormalizeWord).Where(w => w.Length > 0).Distinct()
            .Take(MaxMeaningWords).ToList();
        if (words.Count == 0) return new Dictionary<string, GlossEntryDto>();
        return await gloss.EnrichAsync(words, allowAi: false, VideoGlossService.GlossDeadline, ct);
    }
}
