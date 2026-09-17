using System.Security.Cryptography;
using System.Text;
using StackExchange.Redis;
using ToeicSpace.Identity.Application.Interfaces.Security;
using ToeicSpace.Identity.Infrastructure.Services.Security;

namespace ToeicSpace.Identity.Infrastructure.Services.Caching;

public sealed class RedisLoginAttemptLimiter : ILoginAttemptLimiter
{
    private const string KeyPrefix = "identity:login-failures:";

    private readonly IDatabase _database;
    private readonly LoginLockoutOptions _options;

    public RedisLoginAttemptLimiter(
        IConnectionMultiplexer connectionMultiplexer,
        LoginLockoutOptions options)
    {
        if (options.MaxFailedAttempts <= 0 || options.LockoutDuration <= TimeSpan.Zero)
        {
            throw new InvalidOperationException(
                "LoginLockout:MaxFailedAttempts and LoginLockout:LockoutDuration must be greater than zero.");
        }

        _database = connectionMultiplexer.GetDatabase();
        _options = options;
    }

    public async Task<bool> IsLockedOutAsync(
        string normalizedEmail,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var failures = await _database.StringGetAsync(GetKey(normalizedEmail))
            .WaitAsync(cancellationToken);

        return failures.TryParse(out long count) && count >= _options.MaxFailedAttempts;
    }

    public async Task RecordFailureAsync(
        string normalizedEmail,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        // The window starts at the first failure; the lockout itself always lasts the full duration.
        const string script = """
            local failures = redis.call('INCR', KEYS[1])
            if failures == 1 or failures >= tonumber(ARGV[2]) then
                redis.call('EXPIRE', KEYS[1], ARGV[1])
            end
            return failures
            """;

        await _database.ScriptEvaluateAsync(
                script,
                [GetKey(normalizedEmail)],
                [(long)_options.LockoutDuration.TotalSeconds, _options.MaxFailedAttempts])
            .WaitAsync(cancellationToken);
    }

    public async Task ResetAsync(
        string normalizedEmail,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        await _database.KeyDeleteAsync(GetKey(normalizedEmail))
            .WaitAsync(cancellationToken);
    }

    // Emails are hashed so Redis never stores who tried to sign in.
    private static RedisKey GetKey(string normalizedEmail)
        => KeyPrefix + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(normalizedEmail)));
}
