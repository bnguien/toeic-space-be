using ToeicSpace.Content.Domain.Entities;

namespace ToeicSpace.Content.Application.Interfaces.Repositories;

public interface IVideoLearnerStateRepository
{
    Task<VideoLearnerState?> GetAsync(Guid learnerId, CancellationToken ct);
    Task<VideoLearnerState?> GetForUpdateAsync(Guid learnerId, CancellationToken ct);
    void Add(VideoLearnerState state);
}
