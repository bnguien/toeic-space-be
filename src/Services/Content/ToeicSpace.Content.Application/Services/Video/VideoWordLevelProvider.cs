using ToeicSpace.Content.Application.Interfaces.Services.Video;
using ToeicSpace.Content.Domain.Rules.Videos;
using ToeicSpace.Content.Domain.ValueObjects;

namespace ToeicSpace.Content.Application.Services.Video;

/// <summary>Word levels of transcripts (Studychill <c>vocab_levels</c>), cached 24 h per (video, language).</summary>
public sealed class VideoWordLevelProvider(IVideoWordLevelSource source, IVideoCache cache)
{
    public static readonly TimeSpan TranscriptLevelsTtl = TimeSpan.FromHours(24);

    public WordLevelTable Table => source.Table;

    public IReadOnlyDictionary<string, string> TranscriptLevels(string videoId, string lang, IReadOnlyList<SubtitleCue> cues)
    {
        var key = $"video:levels:{videoId}:{lang}";
        if (cache.TryGet<IReadOnlyDictionary<string, string>>(key, out var cached) && cached is not null) return cached;
        var levels = WordLevelRules.LevelsOfText(cues.Select(c => c.Text), source.Table);
        cache.Set<IReadOnlyDictionary<string, string>>(key, levels, TranscriptLevelsTtl);
        return levels;
    }
}
