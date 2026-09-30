using StackExchange.Redis;
using ToeicSpace.Identity.Application.Interfaces.Caching;
using ToeicSpace.Identity.Application.Interfaces.Security;
using ToeicSpace.Identity.Application.Models;
using ToeicSpace.Identity.Application.Options;

namespace ToeicSpace.Identity.Infrastructure.Services.Caching;

public sealed class RedisPasswordResetStore(
    IConnectionMultiplexer connection,
    IOtpHasher hasher,
    PasswordResetOptions options) : IPasswordResetStore
{
    private readonly IDatabase _database = connection.GetDatabase();

    public async Task<string?> BeginRequestAsync(string email, CancellationToken cancellationToken)
    {
        var requestId = Guid.NewGuid().ToString("N");
        var key = GetEmailKey(email);
        const string script = """
            if not redis.call('SET', KEYS[2], '1', 'EX', ARGV[2], 'NX') then return 0 end
            redis.call('DEL', KEYS[1])
            redis.call('HSET', KEYS[1], 'requestId', ARGV[1])
            redis.call('EXPIRE', KEYS[1], ARGV[3])
            return 1
            """;
        var result = await _database.ScriptEvaluateAsync(script,
            [key, key + ":cooldown"],
            [requestId, options.ResendCooldownSeconds, options.OtpLifetimeMinutes * 60])
            .WaitAsync(cancellationToken);
        return (long)result == 1 ? requestId : null;
    }

    public async Task<bool> SetOtpAsync(string email, string requestId, Guid userId,
        string credentialStamp, string otpHash, CancellationToken cancellationToken)
    {
        const string script = """
            if redis.call('HGET', KEYS[1], 'requestId') ~= ARGV[1] then return 0 end
            if redis.call('HEXISTS', KEYS[1], 'userId') == 1 then return 0 end
            redis.call('HSET', KEYS[1], 'userId', ARGV[2], 'stamp', ARGV[3],
                'otpHash', ARGV[4], 'attempts', '0')
            return 1
            """;
        var result = await _database.ScriptEvaluateAsync(script, [GetEmailKey(email)],
            [requestId, userId.ToString("N"), credentialStamp, otpHash]).WaitAsync(cancellationToken);
        return (long)result == 1;
    }

    public async Task<bool> VerifyAsync(string email, string otpHash, string tokenHash,
        CancellationToken cancellationToken)
    {
        // OTP consumption and grant creation are one atomic operation. Failed guesses never extend TTL.
        const string script = """
            local actual = redis.call('HGET', KEYS[1], 'otpHash')
            if not actual then return 0 end
            if actual ~= ARGV[1] then
                if redis.call('HINCRBY', KEYS[1], 'attempts', 1) >= tonumber(ARGV[3]) then
                    redis.call('DEL', KEYS[1])
                end
                return 0
            end
            redis.call('HDEL', KEYS[1], 'otpHash')
            redis.call('HSET', KEYS[1], 'tokenHash', ARGV[2])
            redis.call('EXPIRE', KEYS[1], ARGV[4])
            redis.call('SET', KEYS[2], KEYS[1], 'EX', ARGV[4])
            return 1
            """;
        var result = await _database.ScriptEvaluateAsync(script,
            [GetEmailKey(email), GetTokenKey(tokenHash)],
            [otpHash, tokenHash, options.MaximumFailedAttempts, options.ResetLifetimeMinutes * 60])
            .WaitAsync(cancellationToken);
        return (long)result == 1;
    }

    public async Task<PasswordResetGrant?> GetGrantAsync(string tokenHash, CancellationToken cancellationToken)
    {
        const string script = """
            local key = redis.call('GET', KEYS[1])
            if not key or redis.call('HGET', key, 'tokenHash') ~= ARGV[1] then return {} end
            return redis.call('HMGET', key, 'userId', 'stamp')
            """;
        var result = await _database.ScriptEvaluateAsync(script, [GetTokenKey(tokenHash)], [tokenHash])
            .WaitAsync(cancellationToken);
        var values = (RedisResult[]?)result;
        return values is { Length: 2 } && Guid.TryParseExact(values[0].ToString(), "N", out var userId)
            ? new PasswordResetGrant(userId, values[1].ToString())
            : null;
    }

    public async Task<bool> ConsumeGrantAsync(string tokenHash, CancellationToken cancellationToken)
    {
        const string script = """
            local key = redis.call('GET', KEYS[1])
            if not key or redis.call('HGET', key, 'tokenHash') ~= ARGV[1] then return 0 end
            redis.call('DEL', key, KEYS[1])
            return 1
            """;
        var result = await _database.ScriptEvaluateAsync(script, [GetTokenKey(tokenHash)], [tokenHash])
            .WaitAsync(cancellationToken);
        return (long)result == 1;
    }

    private string GetEmailKey(string email) => $"identity:password-reset:email:{hasher.Hash(email)}";

    private static string GetTokenKey(string tokenHash) => $"identity:password-reset:token:{tokenHash}";
}
