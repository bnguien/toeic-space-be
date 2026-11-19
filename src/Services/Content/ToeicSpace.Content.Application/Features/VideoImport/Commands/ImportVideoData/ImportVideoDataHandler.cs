using System.Text.Json;
using ToeicSpace.Content.Application.DTOs.Videos;
using ToeicSpace.Content.Application.Interfaces;
using ToeicSpace.Content.Application.Interfaces.Repositories;
using ToeicSpace.Content.Application.Mappings;
using ToeicSpace.Content.Application.Models.Videos;
using ToeicSpace.Content.Application.Services.Video;
using ToeicSpace.Content.Domain.Entities;
using ToeicSpace.Content.Domain.Enums;

namespace ToeicSpace.Content.Application.Features.VideoImport.Commands.ImportVideoData;

/// <summary>
/// Catalog rows are upserted by <c>VideoId</c> with every field of the snapshot (topic copied onto the row);
/// subtitle documents by <c>VideoId</c>: the language list is replaced, imported tracks are set unless the stored track
/// came from YouTube/translation and the file has none for that language. Each file is one transaction; re-importing
/// the same files reports everything as unchanged.
/// </summary>
public sealed class ImportVideoDataHandler(IVideoCatalogRepository catalog, IVideoSubtitleRepository subtitles,
    IUnitOfWork unitOfWork, VideoCatalogCache catalogCache, TimeProvider clock)
    : IRequestHandler<ImportVideoDataCommand, VideoImportResultDto>
{
    private const int Batch = 500;

    public async Task<VideoImportResultDto> Handle(ImportVideoDataCommand request, CancellationToken ct)
    {
        var parsed = request.Files.Select(VideoImportParser.Parse).ToList();
        var videos = new Counter();
        var documents = new Counter();
        var tracks = new Counter();
        foreach (var file in parsed)
        {
            await using var transaction = await unitOfWork.BeginTransactionAsync(ct);
            if (file.Catalog is not null) await ImportCatalogAsync(file.Catalog, videos, ct);
            if (file.Subtitles is not null) await ImportSubtitlesAsync(file.Subtitles, documents, tracks, ct);
            await unitOfWork.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
        }
        catalogCache.Clear();
        return new VideoImportResultDto(videos.ToDto(), documents.ToDto(), tracks.ToDto(),
            parsed.Select(f => f.FileName).ToList());
    }

    private async Task ImportCatalogAsync(ImportedCatalog file, Counter counter, CancellationToken ct)
    {
        var topics = file.Topics.ToDictionary(t => t.Slug);
        var now = clock.GetUtcNow().UtcDateTime;
        foreach (var chunk in file.Videos.Chunk(Batch))
        {
            var existing = await catalog.GetManyForUpdateAsync(chunk.Select(v => v.VideoId).ToList(), ct);
            foreach (var v in chunk)
            {
                var topic = topics[v.Topic];
                var item = existing.GetValueOrDefault(v.VideoId);
                var isNew = item is null;
                item ??= new VideoCatalogItem { VideoId = v.VideoId, CreatedAt = now };
                var before = Snapshot(item);
                item.TopicSlug = topic.Slug;
                item.TopicTitle = topic.Title;
                item.TopicPosition = topic.Position;
                item.Title = v.Title;
                item.ChannelHandle = v.ChannelHandle;
                item.ChannelTitle = v.ChannelTitle;
                item.Duration = v.Duration;
                item.Level = v.Level;
                item.Thumbnail = v.Thumbnail;
                item.ViewCount = v.ViewCount;
                item.Position = v.Position;
                item.IsActive = v.IsActive;
                item.SubtitleStatus = VideoMappingExtensions.ToSubtitleStatus(v.SubtitleStatus);
                item.EmbedStatus = VideoMappingExtensions.ToEmbedStatus(v.EmbedStatus);
                item.EmbedReason = v.EmbedReason;
                item.Difficulty = v.Difficulty;
                item.Cefr = v.Cefr;
                item.Track = VideoMappingExtensions.ToVideoTrack(v.Track);
                item.LevelSource = VideoMappingExtensions.ToLevelSource(v.LevelSource);
                if (isNew)
                {
                    item.UpdatedAt = now;
                    catalog.Add(item);
                    counter.Created++;
                }
                else if (before != Snapshot(item))
                {
                    item.UpdatedAt = now;
                    counter.Updated++;
                }
                else counter.Unchanged++;
            }
        }
    }

    private async Task ImportSubtitlesAsync(ImportedSubtitles file, Counter documents, Counter trackCounter,
        CancellationToken ct)
    {
        var languages = file.Languages.GroupBy(l => l.VideoId).ToDictionary(g => g.Key, g => g.Last());
        var tracksByVideo = file.Tracks.GroupBy(t => t.VideoId).ToDictionary(g => g.Key, g => g.ToList());
        var ids = languages.Keys.Union(tracksByVideo.Keys).Distinct().ToList();
        var now = clock.GetUtcNow().UtcDateTime;
        foreach (var chunk in ids.Chunk(Batch))
        {
            var existing = await subtitles.GetManyForUpdateAsync(chunk, ct);
            foreach (var videoId in chunk)
            {
                var row = existing.GetValueOrDefault(videoId);
                var isNew = row is null;
                row ??= new VideoSubtitle { VideoId = videoId, CreatedAt = now };
                var beforeLanguages = (row.Languages, row.Title, row.Duration, row.OriginalLanguage);
                var beforeTracks = row.Tracks;
                if (languages.TryGetValue(videoId, out var list))
                {
                    SubtitleStore.ApplyLanguages(row, list.Manual, list.Auto, list.Title, list.Duration, list.Language);
                    row.LanguagesFetchedAt ??= now;
                }
                var stored = SubtitleStore.ReadTracks(row.Tracks);
                foreach (var track in tracksByVideo.GetValueOrDefault(videoId) ?? [])
                {
                    var key = stored.Keys.FirstOrDefault(k => string.Equals(k, track.Lang, StringComparison.OrdinalIgnoreCase));
                    if (key is null)
                    {
                        stored[track.Lang] = new StoredTrack(SubtitleTrackSource.Imported, track.Cues);
                        trackCounter.Created++;
                    }
                    else if (JsonSerializer.Serialize(stored[key].Cues, SubtitleStore.Json)
                             == JsonSerializer.Serialize(track.Cues, SubtitleStore.Json))
                        trackCounter.Unchanged++;
                    else
                    {
                        stored.Remove(key);
                        stored[track.Lang] = new StoredTrack(SubtitleTrackSource.Imported, track.Cues);
                        trackCounter.Updated++;
                    }
                }
                var newTracks = SubtitleStore.WriteTracks(stored);
                if (isNew || newTracks != beforeTracks) row.Tracks = newTracks;
                if (isNew)
                {
                    row.UpdatedAt = now;
                    subtitles.Add(row);
                    documents.Created++;
                }
                else if (beforeLanguages != (row.Languages, row.Title, row.Duration, row.OriginalLanguage)
                         || newTracks != beforeTracks)
                {
                    row.UpdatedAt = now;
                    documents.Updated++;
                }
                else documents.Unchanged++;
            }
        }
    }

    private static string Snapshot(VideoCatalogItem x) => string.Join('\u001f', x.TopicSlug, x.TopicTitle, x.TopicPosition,
        x.Title, x.ChannelHandle, x.ChannelTitle, x.Duration, x.Level, x.Thumbnail, x.ViewCount, x.Position, x.IsActive,
        x.SubtitleStatus, x.EmbedStatus, x.EmbedReason, x.Difficulty, x.Cefr, x.Track, x.LevelSource);

    private sealed class Counter
    {
        public int Created { get; set; }
        public int Updated { get; set; }
        public int Unchanged { get; set; }
        public ImportCountsDto ToDto() => new(Created, Updated, Unchanged);
    }
}
