using System.Collections.Concurrent;
using System.Net;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ToeicSpace.Content.Infrastructure.Services.Enrichment.Gemini;

/// <summary>Why a Gemini call failed; decides the key cooldown.</summary>
public enum GeminiFailure { RateLimited, Unauthorized, BadRequest, Transient }

/// <summary>
/// In-memory singleton rotating the Gemini API keys. Each call starts at the next key (round robin); healthy keys
/// are tried first, then suspended keys by soonest release. Cooldowns per (key, model): rate limit/quota 30 min,
/// auth 6 h, not found/invalid argument 2 min, network/5xx 10 min after 3 consecutive failures; a success resets the
/// key. Keys are never logged, only their <c>#index</c>.
/// </summary>
public sealed class GeminiKeyPool
{
    public const int TransientThreshold = 3;
    public static readonly TimeSpan RateLimitCooldown = TimeSpan.FromMinutes(30);
    public static readonly TimeSpan UnauthorizedCooldown = TimeSpan.FromHours(6);
    public static readonly TimeSpan BadRequestCooldown = TimeSpan.FromMinutes(2);
    public static readonly TimeSpan TransientCooldown = TimeSpan.FromMinutes(10);

    private readonly IReadOnlyList<string> _keys;
    private readonly TimeProvider _clock;
    private readonly ILogger<GeminiKeyPool> _logger;
    private readonly ConcurrentDictionary<(int Index, string Model), KeyState> _states = new();
    private int _cursor = -1;

    public GeminiKeyPool(IOptions<GeminiOptions> options, TimeProvider clock, ILogger<GeminiKeyPool> logger)
    {
        _keys = options.Value.ResolveKeys();
        _clock = clock;
        _logger = logger;
    }

    public int Count => _keys.Count;

    public string KeyAt(int index) => _keys[index];

    /// <summary>Key indexes to try for one call, best first. Never empty when keys exist.</summary>
    public IReadOnlyList<int> GetAttemptOrder(string model)
    {
        if (_keys.Count == 0) return [];
        var now = _clock.GetUtcNow();
        var start = (int)((uint)Interlocked.Increment(ref _cursor) % (uint)_keys.Count);
        var rotated = Enumerable.Range(0, _keys.Count).Select(i => (start + i) % _keys.Count).ToArray();
        var healthy = rotated.Where(i => SuspendedUntil(i, model) is not { } until || until <= now);
        var suspended = rotated.Where(i => SuspendedUntil(i, model) is { } until && until > now)
            .OrderBy(i => SuspendedUntil(i, model));
        return healthy.Concat(suspended).ToArray();
    }

    public DateTimeOffset? SuspendedUntil(int index, string model) =>
        _states.TryGetValue((index, model), out var state) ? state.SuspendedUntil : null;

    public void ReportSuccess(int index, string model) => _states.TryRemove((index, model), out _);

    public void ReportFailure(int index, string model, GeminiFailure failure)
    {
        var state = _states.GetOrAdd((index, model), _ => new KeyState());
        TimeSpan? cooldown;
        lock (state)
        {
            cooldown = failure switch
            {
                GeminiFailure.RateLimited => RateLimitCooldown,
                GeminiFailure.Unauthorized => UnauthorizedCooldown,
                GeminiFailure.BadRequest => BadRequestCooldown,
                _ => ++state.TransientFailures >= TransientThreshold ? TransientCooldown : null
            };
            if (cooldown is null) return;
            state.TransientFailures = 0;
            state.SuspendedUntil = _clock.GetUtcNow() + cooldown.Value;
        }
        _logger.LogWarning("Gemini key #{Index} suspended for {Model} for {Minutes} minutes ({Failure})",
            index, model, cooldown.Value.TotalMinutes, failure);
    }

    /// <summary>Maps an HTTP status and/or Gemini error body to a failure class.</summary>
    public static GeminiFailure Classify(HttpStatusCode? status, string? body)
    {
        bool Has(string marker) => body?.Contains(marker, StringComparison.OrdinalIgnoreCase) == true;
        if (status == HttpStatusCode.TooManyRequests || Has("RESOURCE_EXHAUSTED") || Has("QUOTA"))
            return GeminiFailure.RateLimited;
        if (status is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden || Has("PERMISSION_DENIED")
            || Has("API_KEY_INVALID"))
            return GeminiFailure.Unauthorized;
        if (status is HttpStatusCode.NotFound or HttpStatusCode.BadRequest || Has("NOT_FOUND") || Has("INVALID_ARGUMENT"))
            return GeminiFailure.BadRequest;
        return GeminiFailure.Transient;
    }

    private sealed class KeyState
    {
        public int TransientFailures { get; set; }
        public DateTimeOffset? SuspendedUntil { get; set; }
    }
}
