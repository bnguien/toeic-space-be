using ToeicSpace.Identity.Application.Features.UserManagement.Models;
using ToeicSpace.Identity.Application.Features.UserManagement.Results;
using ToeicSpace.Identity.Domain.Entities;
using ToeicSpace.Identity.Domain.Enums;

namespace ToeicSpace.Identity.Application.Interfaces.Persistence;

public interface IUserRepository
{
    Task<bool> EmailExistsAsync(
        string email,
        CancellationToken cancellationToken);

    Task<bool> PhoneExistsAsync(
        string phone,
        CancellationToken cancellationToken);

    Task<bool> PhoneExistsForOtherUserAsync(
        string phone,
        Guid userId,
        CancellationToken cancellationToken);

    Task<User?> GetByIdAsync(
        Guid userId,
        CancellationToken cancellationToken);

    Task<User?> GetByEmailAsync(
        string email,
        CancellationToken cancellationToken);

    Task<(IReadOnlyList<User> Items, int TotalCount)> SearchActiveTeachersAsync(
        string? search,
        int page,
        int pageSize,
        CancellationToken cancellationToken);

    Task AddAsync(
        User user,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<User>> GetInactiveOlderThanAsync(
        DateTime cutoffUtc,
        CancellationToken cancellationToken);

    Task DeleteRangeAsync(
        IReadOnlyCollection<User> users,
        CancellationToken cancellationToken);

    Task<(IReadOnlyList<ManagedUserResult> Items, int TotalCount)> SearchAsync(
        UserSearchCriteria criteria,
        CancellationToken cancellationToken);

    Task<UserCounts> GetCountsAsync(
        UserRole? role,
        DateTime createdSinceUtc,
        CancellationToken cancellationToken);

    /// <summary>
    /// Creation times of every account (deleted ones included) created since the given time.
    /// </summary>
    Task<IReadOnlyList<DateTime>> GetCreationTimesAsync(
        UserRole? role,
        DateTime sinceUtc,
        CancellationToken cancellationToken);

    /// <summary>
    /// Sign-ins and session refreshes of users with the role since the given time.
    /// </summary>
    Task<IReadOnlyList<SignIn>> GetSignInsAsync(
        UserRole? role,
        DateTime sinceUtc,
        CancellationToken cancellationToken);

    /// <summary>
    /// Administrators that can sign in: active, verified and not deleted.
    /// </summary>
    Task<int> CountEffectiveAdministratorsAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Runs the operation in a serializable transaction, so two administrators changing
    /// each other at the same time cannot both pass the "another administrator remains" check.
    /// A lost race is reported as a conflict instead of a server error.
    /// </summary>
    Task<T> ExecuteInSerializableTransactionAsync<T>(
        Func<CancellationToken, Task<T>> operation,
        CancellationToken cancellationToken);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}
