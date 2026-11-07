using System.Text.Json.Serialization;
using ToeicSpace.Content.Application.DTOs.Videos;

namespace ToeicSpace.Content.Application.Features.VideoSubtitles.Commands.ResolveVideo;

/// <summary>A pasted link or bare id → video, its subtitle languages; records the video in "Video của tôi".</summary>
public sealed record ResolveVideoCommand(string Url, [property: JsonIgnore] Guid LearnerId = default)
    : IRequest<VideoResolveDto>
{
    public const int MaxUrlLength = 500;
}
