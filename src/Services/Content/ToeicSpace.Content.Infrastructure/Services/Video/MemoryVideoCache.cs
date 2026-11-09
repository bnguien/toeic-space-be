using Microsoft.Extensions.Caching.Memory;
using ToeicSpace.Content.Application.Interfaces.Services.Video;

namespace ToeicSpace.Content.Infrastructure.Services.Video;

/// <summary>Process memory cache of the video feature (single API instance; see docs/video.md).</summary>
public sealed class MemoryVideoCache(IMemoryCache cache) : IVideoCache
{
    private readonly Lock _gate = new();

    public bool TryGet<T>(string key, out T? value)
    {
        if (cache.TryGetValue(key, out var stored) && stored is T typed)
        {
            value = typed;
            return true;
        }
        value = default;
        return false;
    }

    public void Set<T>(string key, T value, TimeSpan ttl) => cache.Set(key, value, ttl);

    public bool TryAdd<T>(string key, T value, TimeSpan ttl)
    {
        lock (_gate)
        {
            if (cache.TryGetValue(key, out _)) return false;
            cache.Set(key, value, ttl);
            return true;
        }
    }

    public void Remove(string key) => cache.Remove(key);
}
