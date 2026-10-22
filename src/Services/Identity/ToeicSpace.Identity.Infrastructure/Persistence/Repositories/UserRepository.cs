using System.Data;
using ToeicSpace.Identity.Application.Features.UserManagement.Models;
using ToeicSpace.Identity.Application.Features.UserManagement.Results;
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

    public async Task<(IReadOnlyList<ManagedUserResult> Items, int TotalCount)> SearchAsync(
        UserSearchCriteria criteria,
        CancellationToken cancellationToken)
    {
        var query = _dbContext.Users.AsNoTracking();

        query = criteria.Deleted
            ? query.Where(user => user.DeletedAt != null)
            : query.Where(user => user.DeletedAt == null);

        if (criteria.Role is { } role)
        {
            query = query.Where(user => user.Role == role);
        }

        if (criteria.Status is { } status)
        {
            query = query.Where(user => user.Status == status);
        }

        if (criteria.Search is { } search)
        {
            query = query.Where(user =>
                user.FullName.Contains(search) ||
                user.Email.Contains(search) ||
                (user.Phone != null && user.Phone.Contains(search)));
        }

        var totalCount = await query.CountAsync(cancellationToken);

        // The ID tie-breaker keeps paging stable when several users share a timestamp or name.
        query = criteria.Sort switch
        {
            UserSortOrder.Oldest => query.OrderBy(user => user.CreatedAt).ThenBy(user => user.Id),
            UserSortOrder.Name => query.OrderBy(user => user.FullName).ThenBy(user => user.Id),
            // Users who never signed in have no token and sort last.
            UserSortOrder.LastActive => query
                .OrderByDescending(user => _dbContext.UserTokens
                    .Where(token => token.UserId == user.Id && token.Type == TokenType.RefreshToken)
                    .Max(token => (DateTime?)token.CreatedAt))
                .ThenBy(user => user.Id),
            _ => query.OrderByDescending(user => user.CreatedAt).ThenBy(user => user.Id)
        };

        var items = await query
            .Skip((criteria.Page - 1) * criteria.PageSize)
            .Take(criteria.PageSize)
            .Select(user => new ManagedUserResult(
                user.Id,
                user.FullName,
                user.Email,
                user.Phone,
                user.AvatarUrl,
                user.Role.ToString(),
                user.Status.ToString(),
                user.EmailVerifiedAt != null,
                user.CreatedAt,
                user.DeletedAt,
                _dbContext.UserTokens
                    .Where(token => token.UserId == user.Id && token.Type == TokenType.RefreshToken)
                    .Max(token => (DateTime?)token.CreatedAt)))
            .ToListAsync(cancellationToken);

        return (items, totalCount);
    }

    public async Task<UserCounts> GetCountsAsync(
        UserRole? role,
        DateTime createdSinceUtc,
        CancellationToken cancellationToken)
    {
        var users = ByRole(role);
        var current = users.Where(user => user.DeletedAt == null);

        return new UserCounts(
            await current.CountAsync(cancellationToken),
            await current.CountAsync(user => user.Status == UserStatus.Active, cancellationToken),
            await current.CountAsync(user => user.Status == UserStatus.Suspended, cancellationToken),
            await current.CountAsync(user => user.Status == UserStatus.Inactive, cancellationToken),
            await users.CountAsync(user => user.DeletedAt != null, cancellationToken),
            await current.CountAsync(user => user.CreatedAt >= createdSinceUtc, cancellationToken));
    }

    public async Task<IReadOnlyList<DateTime>> GetCreationTimesAsync(
        UserRole? role,
        DateTime sinceUtc,
        CancellationToken cancellationToken)
        => await ByRole(role)
            .Where(user => user.CreatedAt >= sinceUtc)
            .Select(user => user.CreatedAt)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<SignIn>> GetSignInsAsync(
        UserRole? role,
        DateTime sinceUtc,
        CancellationToken cancellationToken)
        => await _dbContext.UserTokens
            .AsNoTracking()
            .Where(token => token.Type == TokenType.RefreshToken && token.CreatedAt >= sinceUtc)
            .Join(
                ByRole(role),
                token => token.UserId,
                user => user.Id,
                (token, _) => new SignIn(token.UserId, token.CreatedAt))
            .ToListAsync(cancellationToken);

    private IQueryable<User> ByRole(UserRole? role)
    {
        var users = _dbContext.Users.AsNoTracking();

        return role is { } selectedRole
            ? users.Where(user => user.Role == selectedRole)
            : users;
    }

    public Task<int> CountEffectiveAdministratorsAsync(CancellationToken cancellationToken)
        => _dbContext.Users
            .AsNoTracking()
            .CountAsync(
                user => user.Role == UserRole.Admin
                    && user.Status == UserStatus.Active
                    && user.EmailVerifiedAt != null
                    && user.DeletedAt == null,
                cancellationToken);

    public async Task<T> ExecuteInSerializableTransactionAsync<T>(
        Func<CancellationToken, Task<T>> operation,
        CancellationToken cancellationToken)
    {
        // MySQL turns the reads into shared locks, so a concurrent writer deadlocks and is rolled back.
        await using var transaction = await _dbContext.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);

        try
        {
            var result = await operation(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return result;
        }
        catch (Exception exception) when (IsLockConflict(exception))
        {
            throw AppException.Conflict(
                "Another administrator changed this account at the same time. Please try again.",
                ErrorCodes.ConcurrentUserUpdate);
        }
    }

    public Task<bool> PhoneExistsForOtherUserAsync(
        string phone,
        Guid userId,
        CancellationToken cancellationToken)
        => _dbContext.Users
            .AsNoTracking()
            .AnyAsync(user => user.Phone == phone && user.Id != userId, cancellationToken);

    // 1213: deadlock, 1205: lock wait timeout.
    private static bool IsLockConflict(Exception exception)
    {
        for (var current = exception; current is not null; current = current.InnerException)
        {
            if (current is MySqlConnector.MySqlException { Number: 1213 or 1205 })
            {
                return true;
            }
        }

        return false;
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
