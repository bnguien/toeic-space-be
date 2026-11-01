using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ToeicSpace.Content.Application.Interfaces.Services.Enrichment;
using ToeicSpace.Content.Application.Models.Enrichment;
using ToeicSpace.Content.Domain.Entities;
using ToeicSpace.Content.Infrastructure.Persistence;
using ToeicSpace.Content.Infrastructure.Persistence.Configurations;

namespace ToeicSpace.Content.Infrastructure.Services.Enrichment;

/// <summary>
/// Tier 1 cache over the <c>WordGlossCache</c> table. Each call uses its own scope (and DbContext) so cache writes
/// never commit, or get rolled back with, the caller's unit of work.
/// </summary>
public sealed class WordGlossCacheRepository(IServiceScopeFactory scopes, EnrichmentSettings settings,
    TimeProvider clock, ILogger<WordGlossCacheRepository> logger) : IWordGlossCache
{
    public async Task<WordGloss?> GetAsync(string normalizedText, WordKind kind, CancellationToken ct)
    {
        if (kind == WordKind.Reject) return null;
        var now = clock.GetUtcNow().UtcDateTime;
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ContentDbContext>();
        var entry = await db.WordGlossCache.AsNoTracking().FirstOrDefaultAsync(x => x.NormalizedText == normalizedText
            && x.Kind == kind.Key() && x.Version == WordGloss.CacheVersion && x.ExpiresAt > now, ct);
        return entry is null ? null : ToGloss(entry, kind);
    }

    public async Task SetAsync(WordGloss gloss, CancellationToken ct)
    {
        if (gloss.Kind == WordKind.Reject || string.IsNullOrWhiteSpace(gloss.MeaningVi)
            || string.IsNullOrWhiteSpace(gloss.NormalizedText)
            || gloss.NormalizedText.Length > WordGlossCacheConfiguration.NormalizedTextMaxLength) return;
        var now = clock.GetUtcNow().UtcDateTime;
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ContentDbContext>();
        var entry = await db.WordGlossCache.FirstOrDefaultAsync(x => x.NormalizedText == gloss.NormalizedText
            && x.Kind == gloss.Kind.Key(), ct);
        if (entry is null)
        {
            entry = new WordGlossCacheEntry { NormalizedText = gloss.NormalizedText, Kind = gloss.Kind.Key() };
            db.WordGlossCache.Add(entry);
        }
        entry.Headword = Cut(gloss.Headword, WordGlossCacheConfiguration.NormalizedTextMaxLength) ?? gloss.NormalizedText;
        entry.Phonetic = Cut(gloss.Phonetic, 100);
        entry.PartOfSpeech = gloss.PartOfSpeech;
        entry.MeaningVi = Cut(gloss.MeaningVi, WordGlossCacheConfiguration.MeaningMaxLength)!;
        entry.DefinitionEn = Cut(gloss.DefinitionEn, WordGlossCacheConfiguration.TextMaxLength);
        entry.Example = Cut(gloss.Example, WordGlossCacheConfiguration.TextMaxLength);
        entry.ExampleVi = Cut(gloss.ExampleVi, WordGlossCacheConfiguration.TextMaxLength);
        entry.Cefr = Cut(gloss.Cefr, 8);
        entry.PhraseType = Cut(gloss.PhraseType, 32);
        entry.Usage = Cut(gloss.Usage, WordGlossCacheConfiguration.NoteMaxLength);
        entry.Explanation = Cut(gloss.Explanation, WordGlossCacheConfiguration.NoteMaxLength);
        entry.LiteralMeaning = Cut(gloss.LiteralMeaning, WordGlossCacheConfiguration.NoteMaxLength);
        entry.Sources = gloss.Sources.ToDictionary(x => x.Key.Key(), x => x.Value.Key());
        entry.Version = WordGloss.CacheVersion;
        entry.CreatedAt = now;
        entry.ExpiresAt = now.AddDays(settings.CacheTtlDays);
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateException ex)
        {
            // Another request cached the same text first; its entry is just as good.
            logger.LogDebug(ex, "Skipped caching gloss {Text}", gloss.NormalizedText);
        }
    }

    private static WordGloss ToGloss(WordGlossCacheEntry x, WordKind kind)
    {
        var gloss = new WordGloss
        {
            Text = x.Headword, NormalizedText = x.NormalizedText, Kind = kind, Headword = x.Headword,
            Phonetic = x.Phonetic, PartOfSpeech = x.PartOfSpeech, MeaningVi = x.MeaningVi, DefinitionEn = x.DefinitionEn,
            Example = x.Example, ExampleVi = x.ExampleVi, Cefr = x.Cefr, PhraseType = x.PhraseType, Usage = x.Usage,
            Explanation = x.Explanation, LiteralMeaning = x.LiteralMeaning
        };
        foreach (var (field, source) in x.Sources)
            if (GlossFields.Parse(field) is { } f && GlossSources.Parse(source) is { } s) gloss.Sources[f] = s;
        return gloss;
    }

    private static string? Cut(string? value, int max) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim().Length <= max ? value.Trim() : value.Trim()[..max];
}
