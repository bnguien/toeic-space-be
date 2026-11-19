using ToeicSpace.Content.Application.DTOs.Videos;
using ToeicSpace.Content.Application.Models.Videos;

namespace ToeicSpace.Content.Application.Features.VideoImport.Commands.ImportVideoData;

/// <summary>Imports files of <c>data/videos</c> (catalog.json, subtitles-NN.json.gz); idempotent upsert by natural key.</summary>
public sealed record ImportVideoDataCommand(IReadOnlyList<VideoImportFile> Files) : IRequest<VideoImportResultDto>
{
    public const int MaxFiles = 20;
    public const int MaxFileBytes = 5 * 1024 * 1024;
}
