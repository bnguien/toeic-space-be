using ToeicSpace.Content.Application.DTOs.ReelBackgrounds;

namespace ToeicSpace.Content.Application.Features.ReelBackgrounds.Queries.GetReelBackgrounds;

/// <summary>Random active background clips for Reel mode.</summary>
public sealed record GetReelBackgroundsQuery(int Count = GetReelBackgroundsQuery.DefaultCount)
    : IRequest<IReadOnlyList<ReelBackgroundDto>>
{
    public const int DefaultCount = 40;
    public const int MaxCount = 60;
}
