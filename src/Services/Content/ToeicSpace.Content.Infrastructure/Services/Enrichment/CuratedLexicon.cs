using Microsoft.EntityFrameworkCore;
using ToeicSpace.Content.Application.Interfaces.Services.Enrichment;
using ToeicSpace.Content.Application.Models.Enrichment;
using ToeicSpace.Content.Domain.Enums;
using ToeicSpace.Content.Infrastructure.Persistence;

namespace ToeicSpace.Content.Infrastructure.Services.Enrichment;

/// <summary>Tier 2: the official roadmap words (OwnerKey = Guid.Empty), exact normalized headword.</summary>
public sealed class CuratedLexicon(ContentDbContext db) : ICuratedLexicon
{
    public async Task<CuratedMatch?> FindAsync(string normalizedText, PartOfSpeech? hint, CancellationToken ct)
    {
        var rows = await db.VocabularyWords.AsNoTracking()
            .Where(x => x.OwnerKey == Guid.Empty && x.NormalizedHeadword == normalizedText)
            .OrderBy(x => x.Id).ToListAsync(ct);
        var word = rows.FirstOrDefault(x => x.PartOfSpeech == hint) ?? rows.FirstOrDefault();
        if (word is null) return null;
        return new CuratedMatch(word.Id, new WordGloss
        {
            Text = word.Headword, NormalizedText = word.NormalizedHeadword, Headword = word.Headword,
            Phonetic = string.IsNullOrWhiteSpace(word.Phonetic) ? null : word.Phonetic,
            PartOfSpeech = word.PartOfSpeech, MeaningVi = word.MeaningVi, DefinitionEn = word.DefinitionEn,
            Example = word.Example, ExampleVi = word.ExampleVi, Cefr = word.Cefr
        });
    }
}
