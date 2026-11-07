using ToeicSpace.Content.Application.DTOs.Videos;
using ToeicSpace.Content.Application.Interfaces.Repositories;
using ToeicSpace.Content.Application.Mappings;

namespace ToeicSpace.Content.Application.Features.VideoWatches.Queries.GetWatchProgress;

public sealed class GetWatchProgressHandler(IVideoWatchRepository watches)
    : IRequestHandler<GetWatchProgressQuery, WatchProgressListDto>
{
    public const int MaxRows = 300;

    public async Task<WatchProgressListDto> Handle(GetWatchProgressQuery request, CancellationToken ct) =>
        new((await watches.GetProgressAsync(request.LearnerId, MaxRows, ct)).Select(x => x.ToProgressDto()).ToList());
}
