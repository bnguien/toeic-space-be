namespace ToeicSpace.Content.Infrastructure.Services.Enrichment;

/// <summary>Opens for 5 minutes after 3 consecutive dictionary failures (singleton, thread-safe).</summary>
public sealed class DictionaryCircuitBreaker(TimeProvider clock)
{
    public const int FailureThreshold = 3;
    public static readonly TimeSpan OpenFor = TimeSpan.FromMinutes(5);

    private readonly Lock _gate = new();
    private int _failures;
    private DateTimeOffset _openUntil;

    public bool IsOpen
    {
        get { lock (_gate) return clock.GetUtcNow() < _openUntil; }
    }

    public void RecordSuccess()
    {
        lock (_gate) _failures = 0;
    }

    /// <summary>Returns true when this failure opened the circuit.</summary>
    public bool RecordFailure()
    {
        lock (_gate)
        {
            if (++_failures < FailureThreshold) return false;
            _failures = 0;
            _openUntil = clock.GetUtcNow() + OpenFor;
            return true;
        }
    }
}
