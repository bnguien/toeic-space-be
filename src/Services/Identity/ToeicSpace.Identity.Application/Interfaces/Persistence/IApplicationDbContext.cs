namespace ToeicSpace.Identity.Application.Interfaces.Persistence;

public interface IApplicationDbContext
{
    Task<int> SaveChangesAsync(
        CancellationToken cancellationToken = default);
}
