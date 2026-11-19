using ToeicSpace.Content.Application.DTOs.Videos;
using ToeicSpace.Content.Application.Interfaces.Repositories;
using ToeicSpace.Content.Application.Mappings;

namespace ToeicSpace.Content.Application.Features.VideoWatches.Queries.GetMyVideos;

public sealed class GetMyVideosHandler(IVideoWatchRepository watches) : IRequestHandler<GetMyVideosQuery, MyVideosDto>
{
    public const int MaxRows = 200;

    public async Task<MyVideosDto> Handle(GetMyVideosQuery request, CancellationToken ct)
    {
        var mine = await watches.GetMineAsync(request.LearnerId, MaxRows, ct);
        var shared = await watches.GetSharedAsync(request.LearnerId, mine.Select(x => x.VideoId).ToList(), MaxRows, ct);
        return new MyVideosDto(mine.Select(x => x.ToDto(mine: true)).ToList(),
            shared.Select(x => x.First.ToDto(mine: false, x.ShareCount)).ToList());
    }
}
