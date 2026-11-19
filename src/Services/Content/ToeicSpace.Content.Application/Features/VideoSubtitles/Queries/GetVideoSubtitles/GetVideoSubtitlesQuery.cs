using ToeicSpace.Content.Application.DTOs.Videos;

namespace ToeicSpace.Content.Application.Features.VideoSubtitles.Queries.GetVideoSubtitles;

/// <summary>Cues of 1–2 languages (<c>langs=en,vi</c>); 202 while the server prepares missing ones.</summary>
public sealed record GetVideoSubtitlesQuery(string VideoId, string? Langs, Guid LearnerId) : IRequest<VideoSubtitlesResult>
{
    public const int MaxLangs = 2;

    public IReadOnlyList<string> LangList => (Langs ?? string.Empty).Split(',')
        .Select(x => x.Trim()).Where(x => x.Length > 0).Distinct().Take(MaxLangs).ToList();
}
