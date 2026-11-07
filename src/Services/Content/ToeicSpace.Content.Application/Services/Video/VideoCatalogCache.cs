using ToeicSpace.Content.Application.Interfaces.Services.Video;

namespace ToeicSpace.Content.Application.Services.Video;

/// <summary>
/// Public catalog pages shared by everyone (home 5 min, path 10 min). <see cref="Clear"/> bumps a version so every
/// cached page is dropped at once when a video is hidden, scored or imported.
/// </summary>
public sealed class VideoCatalogCache(IVideoCache cache)
{
    public static readonly TimeSpan HomeTtl = TimeSpan.FromMinutes(5);
    public static readonly TimeSpan PathTtl = TimeSpan.FromMinutes(10);
    private int _version;

    public async Task<T> GetOrCreateAsync<T>(string name, TimeSpan ttl, Func<Task<T>> factory) where T : class
    {
        var key = $"video:catalog:{Volatile.Read(ref _version)}:{name}";
        if (cache.TryGet<T>(key, out var cached) && cached is not null) return cached;
        var value = await factory();
        cache.Set(key, value, ttl);
        return value;
    }

    public void Clear() => Interlocked.Increment(ref _version);
}
