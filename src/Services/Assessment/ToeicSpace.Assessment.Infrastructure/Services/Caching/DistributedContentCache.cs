using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using ToeicSpace.Assessment.Application.Interfaces.Caching;

namespace ToeicSpace.Assessment.Infrastructure.Services.Caching;

/// <summary>
/// JSON cache on top of IDistributedCache (Redis when configured, in-memory otherwise).
/// Cache failures are logged and never break a request.
/// </summary>
public sealed class DistributedContentCache : IContentCache
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    private readonly IDistributedCache _cache;
    private readonly ContentCacheOptions _options;
    private readonly ILogger<DistributedContentCache> _logger;

    public DistributedContentCache(
        IDistributedCache cache,
        ContentCacheOptions options,
        ILogger<DistributedContentCache> logger)
    {
        _cache = cache;
        _options = options;
        _logger = logger;
    }

    public async Task<T?> GetAsync<T>(
        string key,
        CancellationToken cancellationToken)
        where T : class
    {
        try
        {
            var payload = await _cache.GetAsync(key, cancellationToken);

            return payload is null
                ? null
                : JsonSerializer.Deserialize<T>(payload, SerializerOptions);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            _logger.LogWarning(exception, "Failed to read content cache entry {CacheKey}", key);
            return null;
        }
    }

    public async Task SetAsync<T>(
        string key,
        T value,
        CancellationToken cancellationToken)
        where T : class
    {
        try
        {
            var payload = JsonSerializer.SerializeToUtf8Bytes(value, SerializerOptions);

            await _cache.SetAsync(
                key,
                payload,
                new DistributedCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(_options.ExpirationMinutes)
                },
                cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            _logger.LogWarning(exception, "Failed to write content cache entry {CacheKey}", key);
        }
    }

    public async Task RemoveAsync(
        IEnumerable<string> keys,
        CancellationToken cancellationToken)
    {
        foreach (var key in keys)
        {
            try
            {
                await _cache.RemoveAsync(key, cancellationToken);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                _logger.LogWarning(exception, "Failed to remove content cache entry {CacheKey}", key);
            }
        }
    }
}
