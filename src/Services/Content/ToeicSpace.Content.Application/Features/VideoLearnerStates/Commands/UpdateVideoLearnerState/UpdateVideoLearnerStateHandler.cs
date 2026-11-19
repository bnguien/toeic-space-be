using System.Text.Json;
using ToeicSpace.Content.Application.DTOs.Videos;
using ToeicSpace.Content.Application.Interfaces;
using ToeicSpace.Content.Application.Interfaces.Repositories;
using ToeicSpace.Content.Application.Mappings;
using ToeicSpace.Content.Domain.Entities;

namespace ToeicSpace.Content.Application.Features.VideoLearnerStates.Commands.UpdateVideoLearnerState;

public sealed class UpdateVideoLearnerStateHandler(IVideoLearnerStateRepository states, IUnitOfWork unitOfWork,
    TimeProvider clock) : IRequestHandler<UpdateVideoLearnerStateCommand, VideoLearnerStateDto>
{
    public async Task<VideoLearnerStateDto> Handle(UpdateVideoLearnerStateCommand request, CancellationToken ct)
    {
        var state = await states.GetForUpdateAsync(request.LearnerId, ct);
        if (state is null)
        {
            state = new VideoLearnerState { LearnerId = request.LearnerId };
            states.Add(state);
        }
        if (request.ReaderSettings is { } settings)
            state.ReaderSettings = settings.ValueKind == JsonValueKind.Object ? settings.GetRawText() : null;
        if (request.ListenMode is not null) state.ListenMode = request.ListenMode.Trim().ToLowerInvariant();
        if (request.PathTrack is not null) state.PathTrack = VideoMappingExtensions.ToVideoTrack(request.PathTrack);
        state.UpdatedAt = clock.GetUtcNow().UtcDateTime;
        await unitOfWork.SaveChangesAsync(ct);
        return state.ToDto();
    }
}
