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

    public async Task<(IReadOnlyList<User> Items, int TotalCount)> SearchActiveTeachersAsync(
        string? search,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var query = _dbContext.Users.AsNoTracking().Where(user =>
            user.Role == UserRole.Teacher &&
            user.Status == UserStatus.Active &&
            user.DeletedAt == null);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(user => user.FullName.Contains(term) || user.Email.Contains(term));
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query.OrderBy(user => user.FullName).ThenBy(user => user.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (items, totalCount);
    }

    public async Task<(IReadOnlyList<User> Items, int TotalCount)> SearchActiveStudentsAsync(
        string? search,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var query = _dbContext.Users.AsNoTracking().Where(user =>
            user.Role == UserRole.User &&
            user.Status == UserStatus.Active &&
            user.EmailVerifiedAt != null &&
            user.DeletedAt == null);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(user => user.FullName.Contains(term) || user.Email.Contains(term));
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query.OrderBy(user => user.FullName).ThenBy(user => user.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (items, totalCount);
    }

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
