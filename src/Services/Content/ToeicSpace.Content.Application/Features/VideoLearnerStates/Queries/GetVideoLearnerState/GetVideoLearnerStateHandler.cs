using ToeicSpace.Content.Application.DTOs.Videos;
using ToeicSpace.Content.Application.Interfaces.Repositories;
using ToeicSpace.Content.Application.Mappings;

namespace ToeicSpace.Content.Application.Features.VideoLearnerStates.Queries.GetVideoLearnerState;

public sealed class GetVideoLearnerStateHandler(IVideoLearnerStateRepository states)
    : IRequestHandler<GetVideoLearnerStateQuery, VideoLearnerStateDto>
{
    public async Task<VideoLearnerStateDto> Handle(GetVideoLearnerStateQuery request, CancellationToken ct) =>
        (await states.GetAsync(request.LearnerId, ct)).ToDto();
}
