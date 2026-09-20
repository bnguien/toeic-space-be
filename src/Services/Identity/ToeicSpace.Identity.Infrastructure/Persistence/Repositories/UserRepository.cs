using ToeicSpace.Identity.Application.Interfaces.Persistence;
using ToeicSpace.Identity.Domain.Entities;
using ToeicSpace.Identity.Domain.Enums;
using ToeicSpace.Identity.Domain.Exceptions;

namespace ToeicSpace.Identity.Infrastructure.Persistence.Repositories;

public sealed class UserRepository : IUserRepository
{
    private readonly IdentityDbContext _dbContext;

    public UserRepository(IdentityDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<bool> EmailExistsAsync(
        string email,
        CancellationToken cancellationToken)
        => _dbContext.Users
            .AsNoTracking()
            .AnyAsync(user => user.Email == email, cancellationToken);

    public Task<bool> PhoneExistsAsync(
        string phone,
        CancellationToken cancellationToken)
        => _dbContext.Users
            .AsNoTracking()
            .AnyAsync(user => user.Phone == phone, cancellationToken);

    public Task<User?> GetByIdAsync(
        Guid userId,
        CancellationToken cancellationToken)
        => _dbContext.Users.FirstOrDefaultAsync(
            user => user.Id == userId,
            cancellationToken);

    public Task<User?> GetByEmailAsync(
        string email,
        CancellationToken cancellationToken)
        => _dbContext.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(
                user => user.Email == email,
                cancellationToken);

    public async Task AddAsync(
        User user,
        CancellationToken cancellationToken)
        => await _dbContext.Users.AddAsync(user, cancellationToken);

    public async Task<IReadOnlyList<User>> GetInactiveOlderThanAsync(
        DateTime cutoffUtc,
        CancellationToken cancellationToken)
        => await _dbContext.Users
            .Where(user =>
                user.Status == UserStatus.Inactive &&
                user.CreatedAt < cutoffUtc)
            .ToListAsync(cancellationToken);

    public Task DeleteRangeAsync(
        IReadOnlyCollection<User> users,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _dbContext.Users.RemoveRange(users);
        return Task.CompletedTask;
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception)
            when (exception.InnerException is MySqlConnector.MySqlException
            {
                Number: 1062
            })
        {
            throw new DuplicateUserException();
        }
    }
}
