namespace ToeicSpace.Content.Application.Interfaces;

public interface IUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken ct);
    Task<IContentTransaction> BeginTransactionAsync(CancellationToken ct);
}

public interface IContentTransaction : IAsyncDisposable
{
    Task CommitAsync(CancellationToken ct);
}
