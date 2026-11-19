using ToeicSpace.Content.Domain.ValueObjects;

namespace ToeicSpace.Content.Application.DTOs.Videos;

/// <summary>
/// Cues per requested language (same time axis), counts, codes the video does not have and the CEFR level of notable
/// transcript words (lowercase word -> B1/B2/C1/C2/TOEIC).
/// </summary>
public sealed record VideoSubtitlesDto(string VideoId, IReadOnlyDictionary<string, IReadOnlyList<SubtitleCue>> Cues,
    IReadOnlyDictionary<string, int> Counts, IReadOnlyList<string> Unavailable, IReadOnlyDictionary<string, string> Levels);
