using ToeicSpace.Content.Application.Models.Grammar;
using ToeicSpace.Content.Domain.Entities;

namespace ToeicSpace.Content.Application.Services.Grammar;

/// <summary>
/// Process-wide cache of the built grammar content (singleton). Loaded from the database on first use and dropped
/// after every import; a failed load is not cached.
/// </summary>
public sealed class GrammarContentCache
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private volatile GrammarContentBundle? _bundle;

    public async Task<GrammarContentBundle?> GetAsync(
        Func<CancellationToken, Task<IReadOnlyList<GrammarDocument>>> load, CancellationToken ct)
    {
        if (_bundle is { } ready) return ready;
        await _gate.WaitAsync(ct);
        try
        {
            if (_bundle is { } built) return built;
            var documents = await load(ct);
            if (documents.Count == 0) return null;
            return _bundle = GrammarContentBuilder.Build(documents);
        }
        finally
        {
            _gate.Release();
        }
    }

    public void Invalidate() => _bundle = null;
}
