using ToeicSpace.Content.Application.DTOs.Videos;
using ToeicSpace.Content.Application.Interfaces.Services.Enrichment;
using ToeicSpace.Content.Application.Mappings;
using ToeicSpace.Content.Application.Models.Enrichment;
using ToeicSpace.Content.Application.Services.Enrichment;

namespace ToeicSpace.Content.Application.Services.Video;

/// <summary>
/// Transcript word meanings at the cheapest sufficient level (Studychill <c>notebook.gloss</c>), on top of the PBL-75
/// enrichment tiers: <c>cache</c> = stored glosses only; <c>free</c> = cache → official catalog → dictionary ∥
/// translation; <c>ai</c> = signed-in only, AI for the first 10 words, the rest free with a 4 s deadline.
/// </summary>
public sealed class VideoGlossService(WordEnricher enricher, IWordGlossCache glossCache)
{
    public const int MaxWordsPerRequest = 80;
    public const int MaxAiWords = 10;
    public static readonly TimeSpan GlossDeadline = TimeSpan.FromSeconds(8);
    public static readonly TimeSpan OverflowDeadline = TimeSpan.FromSeconds(4);
    public const string LevelCache = "cache";
    public const string LevelFree = "free";
    public const string LevelAi = "ai";

    public static string NormalizeLevel(string? level, bool signedIn)
    {
        var value = (level ?? string.Empty).Trim().ToLowerInvariant();
        if (value == LevelAi) return signedIn ? LevelAi : LevelFree;
        return value == LevelCache ? LevelCache : LevelFree;
    }

    public static List<string> NormalizeWords(IEnumerable<string?>? words) =>
        (words ?? []).Select(w => (w ?? string.Empty).Trim().ToLowerInvariant()).Take(MaxWordsPerRequest)
        .Where(w => w.Length > 0).ToList();

    public async Task<GlossResultDto> GlossAsync(IReadOnlyList<string?>? rawWords, string? rawLevel, bool signedIn,
        CancellationToken ct)
    {
        var level = NormalizeLevel(rawLevel, signedIn);
        var words = NormalizeWords(rawWords);
        if (words.Count == 0) return new GlossResultDto(new Dictionary<string, GlossEntryDto>(), [], level);

        if (level == LevelCache)
        {
            var found = new Dictionary<string, GlossEntryDto>();
            var missing = new List<string>();
            foreach (var word in words.Distinct())
            {
                var input = InputNormalizer.Normalize(word);
                var gloss = input.Kind == WordKind.Reject ? null : await glossCache.GetAsync(input.Key, input.Kind, ct);
                if (gloss is not null && !WordGloss.IsBlank(gloss.MeaningVi)) found[word] = gloss.ToGlossEntry(null);
                else missing.Add(word);
            }
            return new GlossResultDto(found, missing, level);
        }

        if (level == LevelAi)
        {
            var data = await EnrichAsync(words.Take(MaxAiWords).ToList(), allowAi: true, GlossDeadline, ct);
            var missing = new List<string>();
            if (words.Count > MaxAiWords)
            {
                var rest = await EnrichAsync(words.Skip(MaxAiWords).ToList(), allowAi: false, OverflowDeadline, ct);
                missing = MissingMeaning(words.Skip(MaxAiWords), rest);
                foreach (var (word, entry) in data) rest[word] = entry;
                data = rest;
            }
            return new GlossResultDto(data, missing, level);
        }

        var free = await EnrichAsync(words, allowAi: false, GlossDeadline, ct);
        return new GlossResultDto(free, MissingMeaning(words, free), level);
    }

    /// <summary>Free tier (no AI) for a few words; entries for every acceptable word, even when still blank.</summary>
    public async Task<Dictionary<string, GlossEntryDto>> EnrichAsync(IReadOnlyList<string> words, bool allowAi,
        TimeSpan deadline, CancellationToken ct)
    {
        var distinct = words.Distinct().Where(w => InputNormalizer.Normalize(w).Kind != WordKind.Reject).ToList();
        var result = new Dictionary<string, GlossEntryDto>();
        if (distinct.Count == 0) return result;
        var enriched = await enricher.EnrichManyAsync(distinct.Select(w => new EnrichRequest(w)).ToList(),
            new EnrichOptions(allowAi, Interactive: true, deadline), ct);
        for (var i = 0; i < distinct.Count; i++)
            result[distinct[i]] = enriched[i].Gloss.ToGlossEntry(enriched[i].OfficialWordId);
        return result;
    }

    private static List<string> MissingMeaning(IEnumerable<string> words, IReadOnlyDictionary<string, GlossEntryDto> data) =>
        words.Where(w => !data.TryGetValue(w, out var entry) || entry.Meaning.Length == 0).ToList();
}
