using ToeicSpace.Content.Domain.Rules.Videos;

namespace ToeicSpace.Content.Application.Interfaces.Services.Video;

/// <summary>Asks YouTube whether a video plays in the embedded player (oEmbed + watch page). Never throws.</summary>
public interface IVideoEmbedProbe
{
    Task<EmbedCheck> ProbeAsync(string videoId, CancellationToken ct);
}
