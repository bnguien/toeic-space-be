using Microsoft.EntityFrameworkCore;
using ToeicSpace.Content.Application.Interfaces.Repositories;
using ToeicSpace.Content.Application.Models.Videos;
using ToeicSpace.Content.Domain.Entities;
using ToeicSpace.Content.Domain.Enums;

namespace ToeicSpace.Content.Infrastructure.Persistence.Repositories;

public sealed class VideoSubtitleRepository(ContentDbContext db) : IVideoSubtitleRepository
{
    public Task<VideoSubtitle?> GetAsync(string videoId, CancellationToken ct) =>
        PersistenceExceptionTranslator.ExecuteAsync(() => db.VideoSubtitles.AsNoTracking()
            .FirstOrDefaultAsync(x => x.VideoId == videoId, ct));

    public Task<VideoSubtitle?> GetForUpdateAsync(string videoId, CancellationToken ct) =>
        PersistenceExceptionTranslator.ExecuteAsync(() => db.VideoSubtitles.FirstOrDefaultAsync(x => x.VideoId == videoId, ct));

    public async Task<IReadOnlyDictionary<string, VideoSubtitle>> GetManyForUpdateAsync(
        IReadOnlyCollection<string> videoIds, CancellationToken ct) =>
        await PersistenceExceptionTranslator.ExecuteAsync(() => db.VideoSubtitles
            .Where(x => videoIds.Contains(x.VideoId)).ToDictionaryAsync(x => x.VideoId, StringComparer.Ordinal, ct));

    public void Add(VideoSubtitle subtitle) => db.VideoSubtitles.Add(subtitle);
}
