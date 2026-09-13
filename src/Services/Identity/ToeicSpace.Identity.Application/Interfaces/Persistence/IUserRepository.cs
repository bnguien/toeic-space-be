using ToeicSpace.Identity.Domain.Entities;

namespace ToeicSpace.Identity.Application.Interfaces.Persistence;

public interface IUserRepository
{
    Task<bool> EmailExistsAsync(
        string email,
        CancellationToken cancellationToken);

    Task<bool> PhoneExistsAsync(
        string phone,
        CancellationToken cancellationToken);

    Task<User?> GetByIdAsync(
        Guid userId,
        CancellationToken cancellationToken);

    Task<User?> GetByEmailAsync(
        string email,
        CancellationToken cancellationToken);

    Task AddAsync(
        User user,
        CancellationToken cancellationToken);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}
