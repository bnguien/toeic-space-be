namespace ToeicSpace.Content.Domain.Rules;

/// <summary>
/// Fixed keys of the bilingual news feature (same values as Studychill and the frontend catalog): 5 TOEIC stages,
/// 6 topics, licences, length groups and the reading-time estimate.
/// </summary>
public static class NewsCatalog
{
    public const int WordsPerMinute = 160;
    public const int MaxPageSize = 60;
    public const int MaxVocabularyItems = 40;

    /// <summary>TOEIC 250+, 450+, 650+, 800+, 990 (same milestones as the grammar roadmap).</summary>
    public static readonly IReadOnlyList<string> Stages = ["c1", "c2", "c3", "c4", "c5"];

    public static readonly IReadOnlyList<string> Topics =
        ["business", "technology", "lifestyle", "travel", "environment", "education"];

    /// <summary>Text and cover licences that may be published.</summary>
    public static readonly IReadOnlyList<string> Licenses =
        ["public_domain", "cc0", "cc_by", "cc_by_sa", "unsplash", "pexels", "original"];

    public static readonly IReadOnlyList<string> VocabularyLevels = ["easy", "medium", "hard"];
    public const string DefaultVocabularyLevel = "medium";

    /// <summary>Length filter: short &lt; 3 minutes, medium 3–6, long &gt; 6.</summary>
    public static readonly IReadOnlyList<string> Lengths = ["short", "medium", "long"];

    /// <summary>Reader state filter of the signed-in viewer: opened but not completed / completed.</summary>
    public static readonly IReadOnlyList<string> States = ["reading", "done"];

    public static bool IsStage(string? value) => value is not null && Stages.Contains(value);
    public static bool IsTopic(string? value) => value is not null && Topics.Contains(value);
    public static bool IsLicense(string? value) => value is not null && Licenses.Contains(value);

    /// <summary>Reading time: <c>max(1, round(words / 160))</c>, midpoint to even like the Studychill importer.</summary>
    public static int MinutesFor(int wordCount) =>
        Math.Max(1, (int)Math.Round(wordCount / (double)WordsPerMinute, MidpointRounding.ToEven));

    /// <summary>Words of an English sentence as counted by the importer (whitespace separated).</summary>
    public static int CountWords(string text) =>
        text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Length;
}
