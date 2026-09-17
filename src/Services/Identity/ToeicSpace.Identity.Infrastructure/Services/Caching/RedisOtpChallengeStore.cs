using StackExchange.Redis;
using ToeicSpace.Identity.Application.Interfaces.Caching;
using ToeicSpace.Identity.Application.Models;
using ToeicSpace.Identity.Infrastructure.Services.Security;

namespace ToeicSpace.Identity.Infrastructure.Services.Caching;

public sealed class RedisOtpChallengeStore : IOtpChallengeStore
{
    private const string ChallengeKeyPrefix = "otp:challenge:";
    private const string ActiveChallengeKeyPrefix =
        "otp:user-active-challenge:";
    private const string ResendCooldownKeyPrefix = "otp:resend-cooldown:";

    private static readonly RedisValue[] ChallengeFields =
    [
        "userId",
        "otpHash",
        "purpose",
        "failedAttempts"
    ];

    private readonly IDatabase _database;
    private readonly TimeSpan _timeToLive;
    private readonly TimeSpan _resendCooldown;

    public RedisOtpChallengeStore(
        IConnectionMultiplexer connectionMultiplexer,
        OtpOptions options)
    {
        if (options.ChallengeTtlMinutes <= 0)
        {
            throw new InvalidOperationException(
                "Otp:ChallengeTtlMinutes must be greater than zero.");
        }

        if (options.ResendCooldownSeconds <= 0)
        {
            throw new InvalidOperationException(
                "Otp:ResendCooldownSeconds must be greater than zero.");
        }

        _database = connectionMultiplexer.GetDatabase();
        _timeToLive = TimeSpan.FromMinutes(options.ChallengeTtlMinutes);
        _resendCooldown = TimeSpan.FromSeconds(
            options.ResendCooldownSeconds);
    }

    public async Task<string> ReplaceChallengeAsync(
        OtpChallenge challenge,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var challengeId = Guid.NewGuid().ToString("N");

        const string script = """
            local oldChallengeId = redis.call('GET', KEYS[1])
            if oldChallengeId then
                redis.call('DEL', ARGV[7] .. oldChallengeId)
            end

            redis.call('HSET', KEYS[2],
                'userId', ARGV[2],
                'otpHash', ARGV[3],
                'purpose', ARGV[4],
                'failedAttempts', ARGV[5])
            redis.call('EXPIRE', KEYS[2], ARGV[6])
            redis.call('SET', KEYS[1], ARGV[1], 'EX', ARGV[6])
            return 1
            """;

        await _database.ScriptEvaluateAsync(
                script,
                [
                    GetActiveChallengeKey(challenge.UserId),
                    GetChallengeKey(challengeId)
                ],
                [
                    challengeId,
                    challenge.UserId.ToString("N"),
                    challenge.OtpHash,
                    challenge.Purpose,
                    challenge.FailedAttempts,
                    (long)_timeToLive.TotalSeconds,
                    ChallengeKeyPrefix
                ])
            .WaitAsync(cancellationToken);

        return challengeId;
    }

    public async Task<TimeSpan?> TryAcquireResendCooldownAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        const string script = """
            local acquired = redis.call(
                'SET', KEYS[1], '1', 'PX', ARGV[1], 'NX')
            if acquired then
                return 0
            end

            local remaining = redis.call('PTTL', KEYS[1])
            if remaining < 1 then
                return 1
            end

            return remaining
            """;

        var result = await _database.ScriptEvaluateAsync(
                script,
                [GetResendCooldownKey(userId)],
                [(long)_resendCooldown.TotalMilliseconds])
            .WaitAsync(cancellationToken);

        var remainingMilliseconds = (long)result;

        return remainingMilliseconds == 0
            ? null
            : TimeSpan.FromMilliseconds(remainingMilliseconds);
    }

    public async Task<OtpChallenge?> GetAsync(
        string challengeId,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var values = await _database.HashGetAsync(
                GetChallengeKey(challengeId),
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
                    local userId = redis.call('HGET', KEYS[1], 'userId')
                    redis.call('DEL', KEYS[1])
                    local activeKey = ARGV[4] .. userId
                    if redis.call('GET', activeKey) == ARGV[3] then
                        redis.call('DEL', activeKey)
                    end
                end
                return { '0' }
            end

            local values = redis.call('HMGET', KEYS[1], 'userId', 'otpHash', 'purpose', 'failedAttempts')
            redis.call('DEL', KEYS[1])
            local activeKey = ARGV[4] .. values[1]
            if redis.call('GET', activeKey) == ARGV[3] then
                redis.call('DEL', activeKey)
            end
            return { '1', values[1], values[2], values[3], values[4] }
            """;

        var result = await _database.ScriptEvaluateAsync(
                script,
                [GetChallengeKey(challengeId)],
                [
                    otpHash,
                    maximumFailedAttempts,
                    challengeId,
                    ActiveChallengeKeyPrefix
                ])
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
                [GetChallengeKey(challengeId)])
            .WaitAsync(cancellationToken);

        return (long)result;
    }

    public async Task DeleteAsync(
        string challengeId,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        const string script = """
            local userId = redis.call('HGET', KEYS[1], 'userId')
            if not userId then
                return 0
            end

            redis.call('DEL', KEYS[1])
            local activeKey = ARGV[2] .. userId
            if redis.call('GET', activeKey) == ARGV[1] then
                redis.call('DEL', activeKey)
            end
            return 1
            """;

        await _database.ScriptEvaluateAsync(
                script,
                [GetChallengeKey(challengeId)],
                [challengeId, ActiveChallengeKeyPrefix])
            .WaitAsync(cancellationToken);
    }

    private static RedisKey GetChallengeKey(string challengeId)
        => $"{ChallengeKeyPrefix}{challengeId}";

    private static RedisKey GetActiveChallengeKey(Guid userId)
        => $"{ActiveChallengeKeyPrefix}{userId:N}";

    private static RedisKey GetResendCooldownKey(Guid userId)
        => $"{ResendCooldownKeyPrefix}{userId:N}";
}
