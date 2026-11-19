using Microsoft.EntityFrameworkCore;
using ToeicSpace.Content.Application.Interfaces.Repositories;
using ToeicSpace.Content.Application.Models.Videos;
using ToeicSpace.Content.Domain.Entities;
using ToeicSpace.Content.Domain.Enums;

namespace ToeicSpace.Content.Infrastructure.Persistence.Repositories;

public sealed class VideoCatalogRepository(ContentDbContext db) : IVideoCatalogRepository
{
    private IQueryable<VideoCatalogItem> Visible => db.VideoCatalogItems.AsNoTracking()
        .Where(x => x.IsActive && x.SubtitleStatus != VideoSubtitleStatus.Missing && x.EmbedStatus != VideoEmbedStatus.Blocked);

    private static IQueryable<CatalogVideoRow> Project(IQueryable<VideoCatalogItem> query) => query.Select(x =>
        new CatalogVideoRow(x.VideoId, x.TopicSlug, x.TopicTitle, x.TopicPosition, x.Title, x.Duration, x.Level,
            x.ChannelTitle, x.Thumbnail, x.SubtitleStatus, x.Difficulty, x.Cefr, x.Track, x.LevelSource, x.Position));

    public async Task<IReadOnlyList<CatalogVideoRow>> GetVisibleVideosAsync(CancellationToken ct) =>
        await PersistenceExceptionTranslator.ExecuteAsync(() => Project(Visible).ToListAsync(ct));

    public async Task<IReadOnlyList<CatalogVideoRow>> GetVisibleTopicVideosAsync(string topicSlug, int offset, int take,
        CancellationToken ct) =>
        await PersistenceExceptionTranslator.ExecuteAsync(() => Project(Visible.Where(x => x.TopicSlug == topicSlug)
                .OrderBy(x => x.SubtitleStatus == VideoSubtitleStatus.Available ? 0 : 1).ThenBy(x => x.Position)
                .ThenBy(x => x.VideoId))
            .Skip(offset).Take(take).ToListAsync(ct));

    public Task<string?> GetTopicTitleAsync(string topicSlug, CancellationToken ct) =>
        PersistenceExceptionTranslator.ExecuteAsync(() => db.VideoCatalogItems.AsNoTracking()
            .Where(x => x.TopicSlug == topicSlug && x.IsActive).Select(x => x.TopicTitle).FirstOrDefaultAsync(ct));

    public Task<bool> ExistsAsync(string videoId, CancellationToken ct) =>
        PersistenceExceptionTranslator.ExecuteAsync(() => db.VideoCatalogItems.AnyAsync(x => x.VideoId == videoId, ct));

    public Task<VideoCatalogItem?> GetForUpdateAsync(string videoId, CancellationToken ct) =>
        PersistenceExceptionTranslator.ExecuteAsync(() => db.VideoCatalogItems.FirstOrDefaultAsync(x => x.VideoId == videoId, ct));

    public async Task<IReadOnlyDictionary<string, VideoCatalogItem>> GetManyForUpdateAsync(
        IReadOnlyCollection<string> videoIds, CancellationToken ct) =>
        await PersistenceExceptionTranslator.ExecuteAsync(() => db.VideoCatalogItems
            .Where(x => videoIds.Contains(x.VideoId)).ToDictionaryAsync(x => x.VideoId, StringComparer.Ordinal, ct));

    public void Add(VideoCatalogItem item) => db.VideoCatalogItems.Add(item);
}
