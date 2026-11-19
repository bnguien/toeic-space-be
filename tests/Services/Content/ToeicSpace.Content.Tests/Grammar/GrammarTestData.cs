using System.IO.Compression;
using System.Text.Json.Nodes;

namespace ToeicSpace.Content.Tests.Grammar;

/// <summary>The committed grammar bundle (data/grammar/grammar-content.json.gz), loaded once.</summary>
public static class GrammarTestData
{
    private static readonly Lazy<byte[]> Raw = new(() =>
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "data", "grammar", "grammar-content.json.gz")))
            dir = dir.Parent;
        if (dir is null) throw new FileNotFoundException("data/grammar/grammar-content.json.gz not found.");
        return File.ReadAllBytes(Path.Combine(dir.FullName, "data", "grammar", "grammar-content.json.gz"));
    });

    public static byte[] Gzip => Raw.Value;

    public static JsonObject Bundle()
    {
        using var input = new GZipStream(new MemoryStream(Raw.Value), CompressionMode.Decompress);
        return (JsonObject)JsonNode.Parse(input)!;
    }

    /// <summary>Every scored question of the bundle with its cloze flag.</summary>
    public static IEnumerable<(JsonObject Question, bool IsClozeBlank)> Questions(JsonObject bundle)
    {
        IEnumerable<JsonObject> Items(JsonObject exercises) =>
            (exercises["quickChecks"] as JsonObject ?? []).SelectMany(x => (x.Value as JsonArray ?? []).OfType<JsonObject>())
            .Concat((exercises["miniTest"]?["questions"] as JsonArray ?? []).OfType<JsonObject>());
        var items = (bundle["topics"] as JsonArray)!.OfType<JsonObject>().SelectMany(t => Items((JsonObject)t["exercises"]!))
            .Concat((bundle["stages"] as JsonArray)!.OfType<JsonObject>()
                .SelectMany(s => (s["questions"] as JsonArray ?? []).OfType<JsonObject>()));
        foreach (var q in items)
        {
            if (q["kind"]!.GetValue<string>() == "cloze6")
                foreach (var blank in (q["subQuestions"] as JsonArray ?? []).OfType<JsonObject>()) yield return (blank, true);
            else yield return (q, false);
        }
    }
}
