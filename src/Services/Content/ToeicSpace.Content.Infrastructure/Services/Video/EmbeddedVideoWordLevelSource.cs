using System.Text.Json;
using ToeicSpace.Content.Application.Interfaces.Services.Video;
using ToeicSpace.Content.Domain.Rules.Videos;

namespace ToeicSpace.Content.Infrastructure.Services.Video;

/// <summary>
/// Studychill's TOEIC word list with CEFR (2,312 words) and basic word list (1,173), embedded as
/// <c>Resources/word-levels.json</c> (<c>{"levels":{"word":"B2"},"basic":["about",…]}</c>, written by
/// <c>scripts/video_data_pipeline.py build</c>). Loaded once.
/// </summary>
public sealed class EmbeddedVideoWordLevelSource : IVideoWordLevelSource
{
    public const string ResourceName = "ToeicSpace.Content.Infrastructure.Video.word-levels.json";
    private readonly Lazy<WordLevelTable> _table = new(Load);

    public WordLevelTable Table => _table.Value;

    private sealed record Document(Dictionary<string, string>? Levels, List<string>? Basic);

    private static WordLevelTable Load()
    {
        using var stream = typeof(EmbeddedVideoWordLevelSource).Assembly.GetManifestResourceStream(ResourceName);
        if (stream is null) return WordLevelTable.Empty;
        var document = JsonSerializer.Deserialize<Document>(stream, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        var levels = (document?.Levels ?? []).ToDictionary(x => x.Key.Trim().ToLowerInvariant(),
            x => (x.Value ?? string.Empty).Trim().ToUpperInvariant());
        var basic = (document?.Basic ?? []).Select(x => x.Trim().ToLowerInvariant()).Where(x => x.Length > 0).ToHashSet();
        return new WordLevelTable(levels, basic);
    }
}
