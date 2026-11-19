namespace ToeicSpace.Content.Application.Interfaces.Services.Video;

/// <summary>Short-lived process cache of the video feature (catalog pages, quotas, single-flight flags, outcomes).</summary>
public interface IVideoCache
{
    bool TryGet<T>(string key, out T? value);
    void Set<T>(string key, T value, TimeSpan ttl);
    /// <summary>Stores the value only when the key is absent; true when this call stored it.</summary>
    bool TryAdd<T>(string key, T value, TimeSpan ttl);
    void Remove(string key);
}
