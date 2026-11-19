using Microsoft.EntityFrameworkCore;
using ToeicSpace.Content.Application.Interfaces.Repositories;
using ToeicSpace.Content.Application.Models.Videos;
using ToeicSpace.Content.Domain.Entities;
using ToeicSpace.Content.Domain.Enums;

namespace ToeicSpace.Content.Infrastructure.Persistence.Repositories;

public sealed class VideoLearnerStateRepository(ContentDbContext db) : IVideoLearnerStateRepository
{
    public Task<VideoLearnerState?> GetAsync(Guid learnerId, CancellationToken ct) =>
        PersistenceExceptionTranslator.ExecuteAsync(() => db.VideoLearnerStates.AsNoTracking()
            .FirstOrDefaultAsync(x => x.LearnerId == learnerId, ct));

    public Task<VideoLearnerState?> GetForUpdateAsync(Guid learnerId, CancellationToken ct) =>
        PersistenceExceptionTranslator.ExecuteAsync(() => db.VideoLearnerStates
            .FirstOrDefaultAsync(x => x.LearnerId == learnerId, ct));

    public void Add(VideoLearnerState state) => db.VideoLearnerStates.Add(state);
}
