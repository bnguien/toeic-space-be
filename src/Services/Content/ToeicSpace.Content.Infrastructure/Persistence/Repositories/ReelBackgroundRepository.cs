using Microsoft.EntityFrameworkCore;
using ToeicSpace.Content.Application.Interfaces.Repositories;
using ToeicSpace.Content.Domain.Entities;

namespace ToeicSpace.Content.Infrastructure.Persistence.Repositories;

public sealed class ReelBackgroundRepository(ContentDbContext db) : IReelBackgroundRepository
{
    public Task<IReadOnlyList<ReelBackground>> GetActiveAsync(CancellationToken ct) =>
        PersistenceExceptionTranslator.ExecuteAsync<IReadOnlyList<ReelBackground>>(async () =>
            await db.ReelBackgrounds.AsNoTracking().Where(x => x.IsActive).ToListAsync(ct));

    public Task<IReadOnlyList<ReelBackground>> GetByProviderForUpdateAsync(string provider, CancellationToken ct) =>
        PersistenceExceptionTranslator.ExecuteAsync<IReadOnlyList<ReelBackground>>(async () =>
            await db.ReelBackgrounds.Where(x => x.Provider == provider).ToListAsync(ct));

    public void Add(ReelBackground background) => db.ReelBackgrounds.Add(background);
}
