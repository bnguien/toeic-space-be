namespace ToeicSpace.Content.Application.Models.Enrichment;

/// <summary>Tier that produced a field; serialized lowercase (cache, catalog, dictionary, google, mymemory, ai).</summary>
public enum GlossSource { Cache, Catalog, Dictionary, Google, MyMemory, Ai }

public static class GlossSources
{
    public static string Key(this GlossSource source) => source.ToString().ToLowerInvariant();

    public static GlossSource? Parse(string key) =>
        Enum.TryParse<GlossSource>(key, true, out var source) ? source : null;
}
