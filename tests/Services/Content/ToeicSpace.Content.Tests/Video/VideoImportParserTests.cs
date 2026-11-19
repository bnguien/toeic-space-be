using System.IO.Compression;
using System.Text;
using ToeicSpace.Content.Application.Models.Videos;
using ToeicSpace.Content.Application.Services.Video;
using ToeicSpace.Content.Domain.Entities;
using ToeicSpace.Content.Domain.Enums;
using ToeicSpace.Content.Domain.Exceptions;
using ToeicSpace.Content.Domain.ValueObjects;
using Xunit;

namespace ToeicSpace.Content.Tests.Video;

public sealed class VideoImportParserTests
{
    private const string Catalog = """
        {"schemaVersion":1,"topics":[{"slug":"slow-news","title":"Tin tức chậm","position":2,"isActive":true}],
         "videos":[{"videoId":"-abc_DEF123","topic":"slow-news","title":"News","channelHandle":"@bbc","channelTitle":"BBC",
           "duration":300,"level":"B1","thumbnail":"","viewCount":10,"position":0,"isActive":true,
           "subtitleStatus":"available","embedStatus":"playable","embedReason":"","difficulty":40,"cefr":"B1",
           "track":"medium","levelSource":"subtitles"}]}
        """;

    private const string Subtitles = """
        {"schemaVersion":1,
         "languages":[{"videoId":"-abc_DEF123","manual":[{"code":"en","name":"English"},{"code":"en-ehkg1hFWq8A","name":"English - CC"}],
           "auto":[{"code":"en-orig","name":"English (Original)"}],"title":"News","duration":300,"language":"en"}],
         "tracks":[{"videoId":"-abc_DEF123","lang":"en-orig","cues":[[0.5,2,"hello there",[0,0.4]],[3,1,"bye"]]}]}
        """;

    private static VideoImportFile File(string name, string json, bool gzip = false)
    {
        var bytes = Encoding.UTF8.GetBytes(json);
        if (!gzip) return new VideoImportFile(name, bytes);
        using var buffer = new MemoryStream();
        using (var stream = new GZipStream(buffer, CompressionLevel.Optimal, leaveOpen: true)) stream.Write(bytes);
        return new VideoImportFile(name, buffer.ToArray());
    }

    [Fact]
    public void Catalog_file_maps_english_values()
    {
        var data = VideoImportParser.Parse(File("catalog.json", Catalog));
        var video = Assert.Single(data.Catalog!.Videos);
        Assert.Equal(("-abc_DEF123", "slow-news", "available", "subtitles", "medium"),
            (video.VideoId, video.Topic, video.SubtitleStatus, video.LevelSource, video.Track));
        Assert.Equal(2, Assert.Single(data.Catalog.Topics).Position);
        Assert.Null(data.Subtitles);
    }

    [Fact]
    public void Gzip_subtitle_chunk_is_read_and_unusable_language_codes_are_skipped()
    {
        var data = VideoImportParser.Parse(File("subtitles-01.json.gz", Subtitles, gzip: true));
        var list = Assert.Single(data.Subtitles!.Languages);
        Assert.Equal(["en"], list.Manual.Select(x => x.Code));
        var track = Assert.Single(data.Subtitles.Tracks);
        Assert.Equal(2, track.Cues.Count);
        Assert.Equal([0, 0.4], track.Cues[0].Offsets!);
    }

    [Theory]
    [InlineData("{\"schemaVersion\":2,\"videos\":[]}")]
    [InlineData("{\"schemaVersion\":1,\"something\":[]}")]
    [InlineData("not json")]
    [InlineData("{\"schemaVersion\":1,\"topics\":[],\"videos\":[{\"videoId\":\"abc\",\"topic\":\"missing\"}]}")]
    public void Invalid_files_are_rejected(string json)
    {
        var error = Assert.Throws<ContentRuleException>(() => VideoImportParser.Parse(File("bad.json", json)));
        Assert.Equal("invalid_import_file", error.Code);
    }

    [Fact]
    public void Track_documents_round_trip_with_english_keys()
    {
        var tracks = new Dictionary<string, StoredTrack>
        {
            ["vi"] = new(SubtitleTrackSource.Translated, [new SubtitleCue(1, 2, "xin chào")]),
            ["en-orig"] = new(SubtitleTrackSource.Imported, [new SubtitleCue(1, 2, "hello", [0])])
        };
        var json = SubtitleStore.WriteTracks(tracks);
        Assert.Equal("{\"en-orig\":{\"source\":\"imported\",\"cues\":[[1,2,\"hello\",[0]]]},\"vi\":{\"source\":\"translated\",\"cues\":[[1,2,\"xin chào\"]]}}", json);
        var back = SubtitleStore.ReadTracks(json);
        Assert.Equal(SubtitleTrackSource.Translated, back["vi"].Source);
        Assert.Equal("xin chào", back["vi"].Cues[0].Text);
    }

    [Fact]
    public void Language_document_keeps_the_original_language()
    {
        var row = new VideoSubtitle { VideoId = "-abc_DEF123" };
        SubtitleStore.ApplyLanguages(row, [new VideoLanguage("en", "English")], [], "News", 300, "en-US");
        var stored = SubtitleStore.ReadLanguages(row)!;
        Assert.Equal(("News", 300, "en-US", "en"), (stored.Title, stored.Duration, stored.OriginalLanguage, stored.Manual[0].Code));
        Assert.Contains("\"manual\"", row.Languages);
        Assert.Null(SubtitleStore.ReadLanguages(new VideoSubtitle()));
    }
}
