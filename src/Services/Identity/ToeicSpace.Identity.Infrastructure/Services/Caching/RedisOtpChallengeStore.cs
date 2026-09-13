using StackExchange.Redis;
using ToeicSpace.Identity.Application.Interfaces.Caching;
using ToeicSpace.Identity.Application.Models;
using ToeicSpace.Identity.Infrastructure.Services.Security;

namespace ToeicSpace.Identity.Infrastructure.Services.Caching;

public sealed class RedisOtpChallengeStore : IOtpChallengeStore
{
    private const string KeyPrefix = "identity:email-verification:";

    private static readonly RedisValue[] ChallengeFields =
    [
        "userId",
        "otpHash",
        "purpose",
        "failedAttempts"
    ];

    private readonly IDatabase _database;
    private readonly TimeSpan _timeToLive;

    public RedisOtpChallengeStore(
        IConnectionMultiplexer connectionMultiplexer,
        OtpOptions options)
    {
        if (options.ChallengeTtlMinutes <= 0)
        {
            throw new InvalidOperationException(
                "Otp:ChallengeTtlMinutes must be greater than zero.");
        }

        _database = connectionMultiplexer.GetDatabase();
        _timeToLive = TimeSpan.FromMinutes(options.ChallengeTtlMinutes);
    }

    public async Task StoreAsync(
        string challengeId,
        OtpChallenge challenge,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        const string script = """
            redis.call('HSET', KEYS[1],
                'userId', ARGV[1],
                'otpHash', ARGV[2],
                'purpose', ARGV[3],
                'failedAttempts', ARGV[4])
            redis.call('EXPIRE', KEYS[1], ARGV[5])
            return 1
            """;

        await _database.ScriptEvaluateAsync(
                script,
                [GetKey(challengeId)],
                [
                    challenge.UserId.ToString("N"),
                    challenge.OtpHash,
                    challenge.Purpose,
                    challenge.FailedAttempts,
                    (long)_timeToLive.TotalSeconds
                ])
            .WaitAsync(cancellationToken);
    }

    public async Task<OtpChallenge?> GetAsync(
        string challengeId,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var values = await _database.HashGetAsync(
                GetKey(challengeId),
                ChallengeFields)
            .WaitAsync(cancellationToken);

        if (values.Any(value => value.IsNull) ||
            !Guid.TryParseExact(values[0].ToString(), "N", out var userId) ||
            !int.TryParse(values[3].ToString(), out var failedAttempts))
        {
            return null;
        }

        return new OtpChallenge(
            userId,
            values[1].ToString(),
            values[2].ToString(),
            failedAttempts);
    }

    public async Task<OtpChallenge?> ConsumeAsync(
        string challengeId,
        string otpHash,
        int maximumFailedAttempts,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        const string script = """
            local actualHash = redis.call('HGET', KEYS[1], 'otpHash')
            if not actualHash then
                return { '0' }
            end

            if actualHash ~= ARGV[1] then
                local failedAttempts = redis.call('HINCRBY', KEYS[1], 'failedAttempts', 1)
                if failedAttempts >= tonumber(ARGV[2]) then
                    redis.call('DEL', KEYS[1])
                end
                return { '0' }
            end

            local values = redis.call('HMGET', KEYS[1], 'userId', 'otpHash', 'purpose', 'failedAttempts')
            redis.call('DEL', KEYS[1])
            return { '1', values[1], values[2], values[3], values[4] }
            """;

        var result = await _database.ScriptEvaluateAsync(
                script,
                [GetKey(challengeId)],
                [otpHash, maximumFailedAttempts])
            .WaitAsync(cancellationToken);

        var values = (RedisResult[]?)result;
        if (values is null || values.Length != 5 ||
            values[0].ToString() != "1" ||
            !Guid.TryParseExact(values[1].ToString(), "N", out var userId) ||
            !int.TryParse(values[4].ToString(), out var failedAttempts))
        {
            return null;
        }

        return new OtpChallenge(
            userId,
            values[2].ToString(),
            values[3].ToString(),
            failedAttempts);
    }

    public async Task<long> IncrementFailedAttemptsAsync(
        string challengeId,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        const string script = """
            if redis.call('EXISTS', KEYS[1]) == 0 then
                return -1
            end
            return redis.call('HINCRBY', KEYS[1], 'failedAttempts', 1)
            """;

        var result = await _database.ScriptEvaluateAsync(
                script,
                [GetKey(challengeId)])
            .WaitAsync(cancellationToken);

        return (long)result;
    }

    public async Task DeleteAsync(
        string challengeId,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        await _database.KeyDeleteAsync(GetKey(challengeId))
            .WaitAsync(cancellationToken);
    }

    private static RedisKey GetKey(string challengeId)
        => $"{KeyPrefix}{challengeId}";
}
