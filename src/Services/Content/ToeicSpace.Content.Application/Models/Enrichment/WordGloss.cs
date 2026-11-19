using ToeicSpace.Content.Domain.Enums;

namespace ToeicSpace.Content.Application.Models.Enrichment;

/// <summary>
/// Enrichment data of one word or phrase with per-field provenance. Tiers merge into it with
/// <see cref="Fill"/>, which only writes blank fields ("fill-blanks-only").
/// </summary>
public sealed class WordGloss
{
    /// <summary>Bump to invalidate every cached gloss.</summary>
    public const int CacheVersion = 1;

    public string Text { get; set; } = string.Empty;
    public string NormalizedText { get; set; } = string.Empty;
    public WordKind Kind { get; set; } = WordKind.Word;
    public string Headword { get; set; } = string.Empty;
    public string? Phonetic { get; set; }
    public PartOfSpeech? PartOfSpeech { get; set; }
    public string? MeaningVi { get; set; }
    public string? DefinitionEn { get; set; }
    public string? Example { get; set; }
    public string? ExampleVi { get; set; }
    public string? Cefr { get; set; }
    public string? PhraseType { get; set; }
    public string? Usage { get; set; }
    public string? Explanation { get; set; }
    public string? LiteralMeaning { get; set; }
    public Dictionary<GlossField, GlossSource> Sources { get; } = [];

    /// <summary>Word: meaning and (phonetic or part of speech). Phrase: meaning.</summary>
    public bool IsComplete => !IsBlank(MeaningVi) && (Kind == WordKind.Phrase
        || !IsBlank(Phonetic) || PartOfSpeech is not null);

    public bool Has(GlossField field) => field switch
    {
        GlossField.PartOfSpeech => PartOfSpeech is not null,
        _ => !IsBlank(GetText(field))
    };

    public string? GetText(GlossField field) => field switch
    {
        GlossField.Phonetic => Phonetic,
        GlossField.PartOfSpeech => PartOfSpeech?.ToString(),
        GlossField.MeaningVi => MeaningVi,
        GlossField.DefinitionEn => DefinitionEn,
        GlossField.Example => Example,
        GlossField.ExampleVi => ExampleVi,
        GlossField.Cefr => Cefr,
        GlossField.PhraseType => PhraseType,
        GlossField.Usage => Usage,
        GlossField.Explanation => Explanation,
        GlossField.LiteralMeaning => LiteralMeaning,
        _ => null
    };

    /// <summary>Sets a text field only when it is blank and <paramref name="value"/> is not; records the source.</summary>
    public bool FillText(GlossField field, string? value, GlossSource source)
    {
        if (field == GlossField.PartOfSpeech || Has(field) || IsBlank(value)) return false;
        var text = value!.Trim();
        switch (field)
        {
            case GlossField.Phonetic: Phonetic = text; break;
            case GlossField.MeaningVi: MeaningVi = text; break;
            case GlossField.DefinitionEn: DefinitionEn = text; break;
            case GlossField.Example: Example = text; break;
            case GlossField.ExampleVi: ExampleVi = text; break;
            case GlossField.Cefr: Cefr = text; break;
            case GlossField.PhraseType: PhraseType = text; break;
            case GlossField.Usage: Usage = text; break;
            case GlossField.Explanation: Explanation = text; break;
            case GlossField.LiteralMeaning: LiteralMeaning = text; break;
        }
        Sources[field] = source;
        return true;
    }

    public bool FillPartOfSpeech(PartOfSpeech? value, GlossSource source)
    {
        if (PartOfSpeech is not null || value is null) return false;
        PartOfSpeech = value;
        Sources[GlossField.PartOfSpeech] = source;
        return true;
    }

    /// <summary>Merges every non-blank field of <paramref name="other"/> into blank fields of this gloss.</summary>
    public IReadOnlyList<GlossField> Fill(WordGloss? other, GlossSource source)
    {
        var filled = new List<GlossField>();
        if (other is null) return filled;
        foreach (var field in Enum.GetValues<GlossField>())
        {
            var changed = field == GlossField.PartOfSpeech
                ? FillPartOfSpeech(other.PartOfSpeech, source)
                : FillText(field, other.GetText(field), source);
            if (changed) filled.Add(field);
        }
        return filled;
    }

    public static bool IsBlank(string? value) => string.IsNullOrWhiteSpace(value);
}
