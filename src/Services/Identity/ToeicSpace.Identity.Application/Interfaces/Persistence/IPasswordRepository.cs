namespace ToeicSpace.Identity.Application.Interfaces.Persistence;

public interface IPasswordRepository
{
    // Compare-and-swap the credential and revoke tokens in the same transaction.
    Task<bool> TryChangeAsync(Guid userId, string expectedHash, string newHash,
        CancellationToken cancellationToken);
}
