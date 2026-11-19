namespace ToeicSpace.Content.Application.Services.Enrichment;

/// <summary>Rejects empty translations, echoes of the input and provider warning texts.</summary>
public static class TranslationFilter
{
    private static readonly string[] JunkMarkers =
    [
        "MYMEMORY WARNING", "QUERY LENGTH LIMIT", "INVALID LANGUAGE PAIR", "PLEASE SELECT TWO DISTINCT LANGUAGES",
        "NO QUERY SPECIFIED", "YOU USED ALL AVAILABLE FREE TRANSLATIONS", "INVALID EMAIL", "AVAILABLE FREE TRANSLATIONS"
    ];

    public static bool IsJunk(string input, string? output)
    {
        if (string.IsNullOrWhiteSpace(output)) return true;
        var text = output.Trim();
        return string.Equals(text, input.Trim(), StringComparison.OrdinalIgnoreCase)
            || JunkMarkers.Any(marker => text.Contains(marker, StringComparison.OrdinalIgnoreCase));
    }
}
