using ToeicSpace.Content.Application.DTOs.ReelBackgrounds;
using ToeicSpace.Content.Application.Interfaces.Repositories;
using ToeicSpace.Content.Application.Mappings;

namespace ToeicSpace.Content.Application.Features.ReelBackgrounds.Queries.GetReelBackgrounds;

public sealed class GetReelBackgroundsHandler(IReelBackgroundRepository backgrounds)
    : IRequestHandler<GetReelBackgroundsQuery, IReadOnlyList<ReelBackgroundDto>>
{
    public async Task<IReadOnlyList<ReelBackgroundDto>> Handle(GetReelBackgroundsQuery request, CancellationToken ct)
    {
        // The table holds at most a few hundred clips, so shuffling in memory is cheaper than ORDER BY RAND().
        var active = (await backgrounds.GetActiveAsync(ct)).ToArray();
        Random.Shared.Shuffle(active);
        return active.Take(request.Count).Select(x => x.ToDto()).ToArray();
    }
}
