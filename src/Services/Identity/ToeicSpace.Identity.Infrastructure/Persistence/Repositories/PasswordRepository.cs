using ToeicSpace.Identity.Application.Interfaces.Persistence;
using ToeicSpace.Identity.Domain.Enums;

namespace ToeicSpace.Identity.Infrastructure.Persistence.Repositories;

public sealed class PasswordRepository(IdentityDbContext dbContext) : IPasswordRepository
{
    public async Task<bool> TryChangeAsync(Guid userId, string expectedHash, string newHash,
        CancellationToken cancellationToken)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var updated = await dbContext.Users
            .Where(user => user.Id == userId && user.DeletedAt == null
                && user.Status == UserStatus.Active && user.EmailVerifiedAt != null
                && EF.Functions.Collate(user.PasswordHash!, "utf8mb4_bin") == expectedHash)
            .ExecuteUpdateAsync(setters => setters.SetProperty(user => user.PasswordHash, newHash),
                cancellationToken);
        if (updated != 1)
        {
            return false;
        }

        await dbContext.UserTokens.Where(token => token.UserId == userId && !token.IsRevoked)
            .ExecuteUpdateAsync(setters => setters.SetProperty(token => token.IsRevoked, true),
                cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return true;
    }
}
