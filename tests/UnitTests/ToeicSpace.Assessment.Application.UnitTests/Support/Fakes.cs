using System.Collections.Concurrent;
using ToeicSpace.Assessment.Application.Data;
using ToeicSpace.Assessment.Application.Interfaces.Caching;
using ToeicSpace.Assessment.Application.Interfaces.Security;
using ToeicSpace.BuildingBlocks.Messaging;

namespace ToeicSpace.Assessment.Application.UnitTests.Support;

public sealed class FakeCurrentUser : ICurrentUserService
{
    public static FakeCurrentUser Learner() => new() { UserId = Guid.NewGuid(), IsAuthenticated = true };

    public static FakeCurrentUser ContentManager() => new() { UserId = Guid.NewGuid(), IsAuthenticated = true, CanManageContent = true };

    public Guid? UserId { get; init; }

    public bool IsAuthenticated { get; init; }

    public bool CanManageContent { get; init; }
}

public sealed class FakeEventPublisher : IIntegrationEventPublisher
{
    public List<object> Events { get; } = [];

    public Task PublishAsync<TEvent>(
        TEvent integrationEvent,
        CancellationToken cancellationToken = default)
        where TEvent : class
    {
        Events.Add(integrationEvent);
        return Task.CompletedTask;
    }
}

public sealed class InMemoryContentCache : IContentCache
{
    private readonly ConcurrentDictionary<string, object> _entries = new();

    public IReadOnlyCollection<string> Keys => _entries.Keys.ToList();

    public Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken)
        where T : class
        => Task.FromResult(_entries.TryGetValue(key, out var value) ? value as T : null);

    public Task SetAsync<T>(string key, T value, CancellationToken cancellationToken)
        where T : class
    {
        _entries[key] = value;
        return Task.CompletedTask;
    }

    public Task RemoveAsync(IEnumerable<string> keys, CancellationToken cancellationToken)
    {
        foreach (var key in keys)
        {
            _entries.TryRemove(key, out _);
        }

        return Task.CompletedTask;
    }
}

/// <summary>
/// Substring search used instead of MySQL FULLTEXT in unit tests.
/// </summary>
public sealed class ContainsQuestionSearch : IQuestionSearch
{
    public IQueryable<ToeicQuestion> Apply(IQueryable<ToeicQuestion> query, string searchTerm)
        => query.Where(question => question.QuestionText != null && question.QuestionText.Contains(searchTerm));
}

public sealed class FixedTimeProvider : TimeProvider
{
    public static readonly DateTimeOffset DefaultNow = new(2026, 9, 14, 8, 0, 0, TimeSpan.Zero);

    public DateTimeOffset Now { get; set; } = DefaultNow;

    public override DateTimeOffset GetUtcNow() => Now;
}
