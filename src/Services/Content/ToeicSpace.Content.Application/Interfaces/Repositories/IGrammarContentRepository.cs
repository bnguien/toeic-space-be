using ToeicSpace.Content.Domain.Entities;

namespace ToeicSpace.Content.Application.Interfaces.Repositories;

public interface IGrammarContentRepository
{
    /// <summary>Every stored document (read-only).</summary>
    Task<IReadOnlyList<GrammarDocument>> ListAsync(CancellationToken ct);

    /// <summary>Every stored document, tracked (import).</summary>
    Task<IReadOnlyList<GrammarDocument>> ListForUpdateAsync(CancellationToken ct);

    void Add(GrammarDocument document);
}
