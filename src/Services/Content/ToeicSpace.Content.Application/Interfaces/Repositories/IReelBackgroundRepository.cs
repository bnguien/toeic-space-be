using ToeicSpace.Content.Domain.Entities;

namespace ToeicSpace.Content.Application.Interfaces.Repositories;

public interface IReelBackgroundRepository
{
    Task<IReadOnlyList<ReelBackground>> GetActiveAsync(CancellationToken ct);
    /// <summary>Tracked rows of one provider, active or not.</summary>
    Task<IReadOnlyList<ReelBackground>> GetByProviderForUpdateAsync(string provider, CancellationToken ct);
    void Add(ReelBackground background);
}
