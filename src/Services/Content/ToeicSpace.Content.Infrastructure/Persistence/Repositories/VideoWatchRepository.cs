using Microsoft.EntityFrameworkCore;
using ToeicSpace.Content.Application.Interfaces.Repositories;
using ToeicSpace.Content.Application.Models.Videos;
using ToeicSpace.Content.Domain.Entities;
using ToeicSpace.Content.Domain.Enums;

namespace ToeicSpace.Content.Infrastructure.Persistence.Repositories;

public sealed class VideoWatchRepository(ContentDbContext db) : IVideoWatchRepository
{
    public Task<VideoWatch?> GetForUpdateAsync(Guid learnerId, string videoId, CancellationToken ct) =>
        PersistenceExceptionTranslator.ExecuteAsync(() => db.VideoWatches
            .FirstOrDefaultAsync(x => x.LearnerId == learnerId && x.VideoId == videoId, ct));

    public Task<VideoWatch?> GetByIdForUpdateAsync(Guid learnerId, Guid id, CancellationToken ct) =>
        PersistenceExceptionTranslator.ExecuteAsync(() => db.VideoWatches
            .FirstOrDefaultAsync(x => x.Id == id && x.LearnerId == learnerId && !x.IsDeleted, ct));

    public async Task<IReadOnlyList<VideoWatch>> GetMineAsync(Guid learnerId, int take, CancellationToken ct) =>
        await PersistenceExceptionTranslator.ExecuteAsync(() => db.VideoWatches.AsNoTracking()
            .Where(x => x.LearnerId == learnerId && !x.IsDeleted)
            .OrderByDescending(x => x.LastOpenedAt).ThenByDescending(x => x.CreatedAt).Take(take).ToListAsync(ct));

    /// <summary>
    /// Grouped by video: the representative is the FIRST learner who shared it (stable name on the card), with the
    /// number of learners sharing it.
    /// </summary>
    public async Task<IReadOnlyList<SharedVideoGroup>> GetSharedAsync(Guid learnerId,
        IReadOnlyCollection<string> excludeVideoIds, int take, CancellationToken ct)
    {
        var rows = await PersistenceExceptionTranslator.ExecuteAsync(() => db.VideoWatches.AsNoTracking()
            .Where(x => x.IsPublic && !x.IsDeleted && x.LearnerId != learnerId && !excludeVideoIds.Contains(x.VideoId))
            .OrderBy(x => x.CreatedAt).ThenBy(x => x.Id).Take(5000).ToListAsync(ct));
        return rows.GroupBy(x => x.VideoId)
            .Select(g => new SharedVideoGroup(g.First(), g.Count()))
            .OrderByDescending(x => x.First.LastOpenedAt).ThenByDescending(x => x.First.CreatedAt)
            .Take(take).ToList();
    }

    public async Task<IReadOnlyList<VideoWatch>> GetOtherSharersAsync(Guid learnerId, string videoId, CancellationToken ct) =>
        await PersistenceExceptionTranslator.ExecuteAsync(() => db.VideoWatches.AsNoTracking()
            .Where(x => x.VideoId == videoId && x.IsPublic && !x.IsDeleted && x.LearnerId != learnerId)
            .OrderBy(x => x.CreatedAt).ThenBy(x => x.Id).ToListAsync(ct));

    public async Task<IReadOnlyList<VideoWatch>> GetProgressAsync(Guid learnerId, int take, CancellationToken ct) =>
        await PersistenceExceptionTranslator.ExecuteAsync(() => db.VideoWatches.AsNoTracking()
            .Where(x => x.LearnerId == learnerId && x.ProgressAt != null)
            .OrderByDescending(x => x.ProgressAt).Take(take).ToListAsync(ct));

    public void Add(VideoWatch watch) => db.VideoWatches.Add(watch);
}
