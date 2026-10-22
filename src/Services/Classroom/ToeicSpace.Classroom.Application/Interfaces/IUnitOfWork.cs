namespace ToeicSpace.Classroom.Application.Interfaces;

public interface IUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
    Task<IClassroomTransaction> BeginTransactionAsync(CancellationToken cancellationToken);
}

public interface IClassroomTransaction : IAsyncDisposable
{
    Task CommitAsync(CancellationToken cancellationToken);
}