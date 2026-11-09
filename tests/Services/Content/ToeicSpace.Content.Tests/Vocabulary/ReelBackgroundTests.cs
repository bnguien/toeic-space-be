using System.Net;
using System.Text;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using ToeicSpace.Content.Application.Features.ReelBackgrounds.Queries.GetReelBackgrounds;
using ToeicSpace.Content.Application.Interfaces;
using ToeicSpace.Content.Application.Interfaces.Repositories;
using ToeicSpace.Content.Domain.Entities;
using ToeicSpace.Content.Infrastructure.Persistence;
using ToeicSpace.Content.Infrastructure.Persistence.Repositories;
using ToeicSpace.Content.Infrastructure.Services.Pixabay;
using Xunit;

namespace ToeicSpace.Content.Tests.Vocabulary;

public sealed class ReelBackgroundTests : IAsyncLifetime
{
    private readonly SqliteConnection _connection = new("DataSource=:memory:");
    private readonly FakePixabay _pixabay = new();
    private readonly FixedTimeProvider _clock = new(new DateTimeOffset(2026, 11, 1, 0, 0, 0, TimeSpan.Zero));
    private ServiceProvider _services = null!;

    public async Task InitializeAsync()
    {
        await _connection.OpenAsync();
        var services = new ServiceCollection();
        services.AddDbContext<ContentDbContext>(o => o.UseSqlite(_connection));
        services.AddScoped<IReelBackgroundRepository, ReelBackgroundRepository>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddHttpClient<PixabayVideoClient>().ConfigurePrimaryHttpMessageHandler(() => _pixabay);
        _services = services.BuildServiceProvider();
        await using var scope = _services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<ContentDbContext>().Database.EnsureCreatedAsync();
    }

    public async Task DisposeAsync()
    {
        await _services.DisposeAsync();
        await _connection.DisposeAsync();
    }

    private PixabayReelBackgroundRefresher Refresher(string? key = "test-key") => new(
        _services.GetRequiredService<IServiceScopeFactory>(),
        Options.Create(new PixabayOptions { ApiKey = key, Queries = ["nature", "ocean"], RequestDelay = TimeSpan.Zero }),
        _clock, NullLogger<PixabayReelBackgroundRefresher>.Instance);

    private async Task<List<ReelBackground>> RowsAsync()
    {
        await using var scope = _services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<ContentDbContext>().ReelBackgrounds.AsNoTracking()
            .OrderBy(x => x.ProviderId).ToListAsync();
    }

    [Fact]
    public async Task Refresh_keeps_calm_clips_prefers_small_rendition_and_ages_out_missing_ones()
    {
        await using (var scope = _services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ContentDbContext>();
            db.ReelBackgrounds.Add(new ReelBackground
            {
                Provider = ReelBackground.PixabayProvider, ProviderId = "999", VideoUrl = "v", PosterUrl = "p",
                PageUrl = "u", Author = "a", MissCount = ReelBackground.MaxMisses - 1
            });
            await db.SaveChangesAsync();
        }
        _pixabay.Body = """
        { "total": 4, "totalHits": 4, "hits": [
          { "id": 101, "pageURL": "https://pixabay.com/videos/lake-101/", "tags": "lake, nature, water", "duration": 20,
            "user": "calm", "videos": {
              "medium": { "url": "https://cdn/101-medium.mp4", "width": 1280, "height": 720, "thumbnail": "https://cdn/101-m.jpg" },
              "small": { "url": "https://cdn/101-small.mp4", "width": 960, "height": 540, "thumbnail": "https://cdn/101-s.jpg" },
              "tiny": { "url": "https://cdn/101-tiny.mp4", "width": 640, "height": 360, "thumbnail": "https://cdn/101-t.jpg" } } },
          { "id": 102, "pageURL": "https://pixabay.com/videos/sea-102/", "tags": "sea", "duration": 30, "user": "wave",
            "videos": { "tiny": { "url": "https://cdn/102-tiny.mp4", "width": 640, "height": 360, "thumbnail": "https://cdn/102-t.jpg" } } },
          { "id": 103, "pageURL": "https://pixabay.com/videos/beach-103/", "tags": "beach, woman, sunset", "duration": 25,
            "user": "x", "videos": { "small": { "url": "https://cdn/103.mp4", "width": 960, "height": 540, "thumbnail": "https://cdn/103.jpg" } } },
          { "id": 104, "pageURL": "https://pixabay.com/videos/rain-104/", "tags": "rain", "duration": 8,
            "user": "x", "videos": { "small": { "url": "https://cdn/104.mp4", "width": 960, "height": 540, "thumbnail": "https://cdn/104.jpg" } } }
        ] }
        """;

        Assert.Equal(2, await Refresher().RunOnceAsync());
        Assert.Equal(2, _pixabay.Requests.Count);
        Assert.All(_pixabay.Requests, x => Assert.Contains("safesearch=true", x));

        var rows = await RowsAsync();
        Assert.Equal(["101", "102", "999"], rows.Select(x => x.ProviderId));
        var lake = rows[0];
        Assert.Equal(("https://cdn/101-small.mp4", "https://cdn/101-s.jpg", "calm", 960, 540, 20, true),
            (lake.VideoUrl, lake.PosterUrl, lake.Author, lake.Width, lake.Height, lake.DurationSeconds, lake.IsActive));
        Assert.Equal("https://cdn/102-tiny.mp4", rows[1].VideoUrl);
        Assert.Equal((ReelBackground.MaxMisses, false), (rows[2].MissCount, rows[2].IsActive));

        var handler = new GetReelBackgroundsHandler(new ReelBackgroundRepository(
            _services.CreateScope().ServiceProvider.GetRequiredService<ContentDbContext>()));
        var picked = await handler.Handle(new GetReelBackgroundsQuery(40), default);
        Assert.Equal(2, picked.Count);
        Assert.DoesNotContain(picked, x => x.VideoUrl == "v");
        Assert.Single(await handler.Handle(new GetReelBackgroundsQuery(1), default));
    }

    [Fact]
    public async Task Failed_refresh_never_counts_misses_and_missing_key_does_nothing()
    {
        Assert.Equal(0, await Refresher(key: null).RunOnceAsync());
        Assert.Empty(_pixabay.Requests);

        _pixabay.Status = HttpStatusCode.TooManyRequests;
        Assert.Equal(0, await Refresher().RunOnceAsync());
        Assert.Empty(await RowsAsync());
    }

    [Fact]
    public void People_tags_are_detected_by_whole_word()
    {
        Assert.True(PixabayReelBackgroundRefresher.ShowsPeople("beach, Man, sunset"));
        Assert.True(PixabayReelBackgroundRefresher.ShowsPeople("young girl"));
        Assert.False(PixabayReelBackgroundRefresher.ShowsPeople("mangrove, manhattan, germany"));
        Assert.False(PixabayReelBackgroundRefresher.ShowsPeople(null));
    }

    private sealed class FakePixabay : HttpMessageHandler
    {
        public string Body { get; set; } = """{ "hits": [] }""";
        public HttpStatusCode Status { get; set; } = HttpStatusCode.OK;
        public List<string> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Requests.Add(request.RequestUri!.ToString());
            return Task.FromResult(new HttpResponseMessage(Status)
            {
                Content = new StringContent(Body, Encoding.UTF8, "application/json")
            });
        }
    }
}
