using Microsoft.EntityFrameworkCore.Storage;
using ToeicSpace.Content.Application.Interfaces;

namespace ToeicSpace.Content.Infrastructure.Persistence;

public sealed class UnitOfWork(ContentDbContext db) : IUnitOfWork
{
    public Task<int> SaveChangesAsync(CancellationToken ct) =>
        PersistenceExceptionTranslator.ExecuteAsync(() => db.SaveChangesAsync(ct));

    public async Task<IContentTransaction> BeginTransactionAsync(CancellationToken ct) =>
        new ContentTransaction(await PersistenceExceptionTranslator.ExecuteAsync(() => db.Database.BeginTransactionAsync(ct)));

    private sealed class ContentTransaction(IDbContextTransaction transaction) : IContentTransaction
    {
        public Task CommitAsync(CancellationToken ct) =>
            PersistenceExceptionTranslator.ExecuteAsync(async () =>
            {
                await transaction.CommitAsync(ct);
                return true;
            });

        public ValueTask DisposeAsync() => transaction.DisposeAsync();
    }
}
