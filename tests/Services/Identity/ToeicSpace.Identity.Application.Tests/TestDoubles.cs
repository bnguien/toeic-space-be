using ToeicSpace.Identity.Application.Interfaces.Caching;
using ToeicSpace.Identity.Application.Interfaces.Messaging;
using ToeicSpace.Identity.Application.Interfaces.Persistence;
using ToeicSpace.Identity.Application.Interfaces.Security;
using ToeicSpace.Identity.Application.Models;
using ToeicSpace.Identity.Domain.Entities;
using ToeicSpace.Identity.Domain.Enums;

namespace ToeicSpace.Identity.Application.Tests;

internal sealed class FakeUserRepository : IUserRepository
{
    public List<User> Users { get; } = [];

    public int SaveChangesCalls { get; private set; }

    public Task<bool> EmailExistsAsync(
        string email,
        CancellationToken cancellationToken)
        => Task.FromResult(Users.Any(user => user.Email == email));

    public Task<bool> PhoneExistsAsync(
        string phone,
        CancellationToken cancellationToken)
        => Task.FromResult(Users.Any(user => user.Phone == phone));

    public Task<User?> GetByIdAsync(
        Guid userId,
        CancellationToken cancellationToken)
        => Task.FromResult(Users.FirstOrDefault(user => user.Id == userId));

    public Task<User?> GetByEmailAsync(
        string email,
        CancellationToken cancellationToken)
        => Task.FromResult(Users.FirstOrDefault(user => user.Email == email));

    public Task AddAsync(
        User user,
        CancellationToken cancellationToken)
    {
        Users.Add(user);
        return Task.CompletedTask;
    }

    public DateTime? LastInactiveOlderThanCutoff { get; private set; }

    public Task<IReadOnlyList<User>> GetInactiveOlderThanAsync(
        DateTime cutoffUtc,
        CancellationToken cancellationToken)
    {
        LastInactiveOlderThanCutoff = cutoffUtc;
        IReadOnlyList<User> users = Users
            .Where(user =>
                user.Status == UserStatus.Inactive &&
                user.CreatedAt < cutoffUtc)
            .ToList();

        return Task.FromResult(users);
    }

    public Task DeleteRangeAsync(
        IReadOnlyCollection<User> users,
        CancellationToken cancellationToken)
    {
        Users.RemoveAll(users.Contains);
        return Task.CompletedTask;
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        SaveChangesCalls++;
        return Task.CompletedTask;
    }
}

internal sealed class FakePasswordHasher : IPasswordHasher
{
    public string Hash(string password) => $"hashed:{password}";
}

internal sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => utcNow;
}

internal sealed class FakeOtpGenerator : IOtpGenerator
{
    public string Generate() => "123456";
}

internal sealed class SequenceOtpGenerator(params string[] otps) : IOtpGenerator
{
    private readonly Queue<string> _otps = new(otps);
    private readonly object _lock = new();

    public string Generate()
    {
        lock (_lock)
        {
            return _otps.Dequeue();
        }
    }
}

internal sealed class ConcurrentOtpGenerator : IOtpGenerator
{
    private int _nextOtp = 100000;

    public string Generate()
        => Interlocked.Increment(ref _nextOtp).ToString("D6");
}

internal sealed class FakeOtpHasher : IOtpHasher
{
    public string Hash(string otp) => $"hash:{otp}";

    public bool Verify(string otp, string expectedHash)
        => string.Equals(Hash(otp), expectedHash, StringComparison.Ordinal);
}

internal sealed class FakeOtpChallengeStore : IOtpChallengeStore
{
    private readonly object _lock = new();
    private readonly HashSet<Guid> _usersInCooldown = [];

    public Dictionary<string, OtpChallenge> Challenges { get; } = [];

    public Dictionary<Guid, string> ActiveChallengeIds { get; } = [];

    public List<string> DeletedChallengeIds { get; } = [];

    public bool EnforceResendCooldown { get; set; } = true;

    public async Task<string> ReplaceChallengeAsync(
        OtpChallenge challenge,
        CancellationToken cancellationToken)
    {
        await Task.Yield();

        lock (_lock)
        {
            if (ActiveChallengeIds.TryGetValue(
                    challenge.UserId,
                    out var oldChallengeId))
            {
                Challenges.Remove(oldChallengeId);
                DeletedChallengeIds.Add(oldChallengeId);
            }

            var challengeId = Guid.NewGuid().ToString("N");
            Challenges[challengeId] = challenge;
            ActiveChallengeIds[challenge.UserId] = challengeId;

            return challengeId;
        }
    }

    public Task<TimeSpan?> TryAcquireResendCooldownAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        lock (_lock)
        {
            if (!EnforceResendCooldown || _usersInCooldown.Add(userId))
            {
                return Task.FromResult<TimeSpan?>(null);
            }

            return Task.FromResult<TimeSpan?>(TimeSpan.FromSeconds(60));
        }
    }

    public void ReleaseResendCooldown(Guid userId)
    {
        lock (_lock)
        {
            _usersInCooldown.Remove(userId);
        }
    }

    public Task<OtpChallenge?> GetAsync(
        string challengeId,
        CancellationToken cancellationToken)
        => Task.FromResult(
            Challenges.TryGetValue(challengeId, out var challenge)
                ? challenge
                : null);

    public Task<long> IncrementFailedAttemptsAsync(
        string challengeId,
        CancellationToken cancellationToken)
    {
        if (!Challenges.TryGetValue(challengeId, out var challenge))
        {
            return Task.FromResult(-1L);
        }

        var failedAttempts = challenge.FailedAttempts + 1;
        Challenges[challengeId] = challenge with
        {
            FailedAttempts = failedAttempts
        };

        return Task.FromResult((long)failedAttempts);
    }

    public Task<OtpChallenge?> ConsumeAsync(
        string challengeId,
        string otpHash,
        int maximumFailedAttempts,
        CancellationToken cancellationToken)
    {
        lock (_lock)
        {
            if (!Challenges.TryGetValue(challengeId, out var challenge))
            {
                return Task.FromResult<OtpChallenge?>(null);
            }

            if (!string.Equals(challenge.OtpHash, otpHash, StringComparison.Ordinal))
            {
                var failedAttempts = challenge.FailedAttempts + 1;
                if (failedAttempts >= maximumFailedAttempts)
                {
                    Challenges.Remove(challengeId);
                    DeletedChallengeIds.Add(challengeId);
                    RemoveActiveChallenge(challenge.UserId, challengeId);
                }
                else
                {
                    Challenges[challengeId] = challenge with
                    {
                        FailedAttempts = failedAttempts
                    };
                }

                return Task.FromResult<OtpChallenge?>(null);
            }

            Challenges.Remove(challengeId);
            DeletedChallengeIds.Add(challengeId);
            RemoveActiveChallenge(challenge.UserId, challengeId);
            return Task.FromResult<OtpChallenge?>(challenge);
        }
    }

    public Task DeleteAsync(
        string challengeId,
        CancellationToken cancellationToken)
    {
        lock (_lock)
        {
            if (Challenges.Remove(challengeId, out var challenge))
            {
                RemoveActiveChallenge(challenge.UserId, challengeId);
            }

            DeletedChallengeIds.Add(challengeId);
            return Task.CompletedTask;
        }
    }

    private void RemoveActiveChallenge(Guid userId, string challengeId)
    {
        if (ActiveChallengeIds.TryGetValue(userId, out var activeChallengeId) &&
            string.Equals(
                activeChallengeId,
                challengeId,
                StringComparison.Ordinal))
        {
            ActiveChallengeIds.Remove(userId);
        }
    }
}

internal sealed class FakeIntegrationEventPublisher
    : IIntegrationEventPublisher
{
    public object? LastEvent { get; private set; }

    public Task PublishAsync<TEvent>(
        TEvent integrationEvent,
        CancellationToken cancellationToken)
        where TEvent : class
    {
        LastEvent = integrationEvent;
        return Task.CompletedTask;
    }
}
