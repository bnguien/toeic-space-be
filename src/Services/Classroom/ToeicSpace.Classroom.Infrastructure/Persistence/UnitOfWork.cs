using Microsoft.EntityFrameworkCore.Storage;
using ToeicSpace.Classroom.Application.Interfaces;

namespace ToeicSpace.Classroom.Infrastructure.Persistence;

public sealed class UnitOfWork(ClassroomDbContext db) : IUnitOfWork
{
    public Task<int> SaveChangesAsync(CancellationToken ct = default) =>
        PersistenceExceptionTranslator.ExecuteAsync(() => db.SaveChangesAsync(ct));

    public async Task<IClassroomTransaction> BeginTransactionAsync(CancellationToken ct) =>
        new ClassroomTransaction(await PersistenceExceptionTranslator.ExecuteAsync(() => db.Database.BeginTransactionAsync(ct)));

    private sealed class ClassroomTransaction(IDbContextTransaction transaction) : IClassroomTransaction
    {
        public Task CommitAsync(CancellationToken ct) =>
            PersistenceExceptionTranslator.ExecuteAsync(() => transaction.CommitAsync(ct));
        public ValueTask DisposeAsync() => transaction.DisposeAsync();
    }
}
