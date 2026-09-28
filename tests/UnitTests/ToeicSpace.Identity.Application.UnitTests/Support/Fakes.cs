using ToeicSpace.Identity.Application.Interfaces.Security;

namespace ToeicSpace.Identity.Application.UnitTests.Support;

public sealed class FixedTimeProvider : TimeProvider
{
    public DateTimeOffset Now { get; set; } = new(2026, 9, 16, 8, 0, 0, TimeSpan.Zero);

    public override DateTimeOffset GetUtcNow() => Now;

    public void Advance(TimeSpan duration) => Now = Now.Add(duration);
}

public sealed class InMemoryLoginAttemptLimiter : ILoginAttemptLimiter
{
    private readonly int _maxFailedAttempts;
    private readonly Dictionary<string, int> _failures = new();

    public InMemoryLoginAttemptLimiter(int maxFailedAttempts)
    {
        _maxFailedAttempts = maxFailedAttempts;
    }

    public int FailuresFor(string email) => _failures.GetValueOrDefault(email);

    public Task<bool> IsLockedOutAsync(string normalizedEmail, CancellationToken cancellationToken)
        => Task.FromResult(FailuresFor(normalizedEmail) >= _maxFailedAttempts);

    public Task RecordFailureAsync(string normalizedEmail, CancellationToken cancellationToken)
    {
        _failures[normalizedEmail] = FailuresFor(normalizedEmail) + 1;
        return Task.CompletedTask;
    }

    public Task ResetAsync(string normalizedEmail, CancellationToken cancellationToken)
    {
        _failures.Remove(normalizedEmail);
        return Task.CompletedTask;
    }
}
