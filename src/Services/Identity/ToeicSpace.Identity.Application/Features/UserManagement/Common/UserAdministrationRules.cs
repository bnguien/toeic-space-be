using ToeicSpace.Identity.Application.Extensions;
using ToeicSpace.Identity.Application.Interfaces.Persistence;
using ToeicSpace.Identity.Domain.Entities;
using ToeicSpace.Identity.Domain.Enums;
using ToeicSpace.Identity.Domain.Exceptions;

namespace ToeicSpace.Identity.Application.Features.UserManagement.Common;

/// <summary>
/// Rules shared by every administrator action on another account.
/// </summary>
public static class UserAdministrationRules
{
    /// <summary>
    /// Administrators change their own account through the profile pages, so a mistake here
    /// cannot lock them out.
    /// </summary>
    public static void EnsureNotSelf(Guid actorId, Guid targetId)
    {
        if (actorId == targetId)
        {
            throw AppException.Forbidden(
                "You cannot perform this action on your own account.",
                ErrorCodes.CannotModifySelf);
        }
    }

    public static async Task<User> GetExistingAsync(
        IUserRepository users,
        Guid userId,
        CancellationToken cancellationToken)
        => await users.GetByIdAsync(userId, cancellationToken)
            ?? throw AppException.NotFound("User", userId, ErrorCodes.UserNotFound);

    public static void EnsureNotDeleted(User user)
    {
        if (user.DeletedAt is not null)
        {
            throw AppException.Conflict(
                "The account is deleted. Restore it first.",
                ErrorCodes.UserDeleted);
        }
    }

    /// <summary>
    /// Call before an action that makes <paramref name="user"/> stop being an effective administrator.
    /// Must run inside the serializable transaction that also saves the change.
    /// </summary>
    public static async Task EnsureAnotherAdministratorRemainsAsync(
        IUserRepository users,
        User user,
        CancellationToken cancellationToken)
    {
        if (!user.IsEffectiveAdministrator())
        {
            return;
        }

        if (await users.CountEffectiveAdministratorsAsync(cancellationToken) <= 1)
        {
            throw AppException.Conflict(
                "At least one active administrator must remain.",
                ErrorCodes.LastAdministrator);
        }
    }

    public static bool TryParseRole(string? value, out UserRole role)
        => TryParseName(value, out role);

    /// <summary>
    /// Administrators can only switch between Active and Suspended.
    /// Inactive means "email not verified" and is reserved for registration.
    /// </summary>
    public static bool TryParseAssignableStatus(string? value, out UserStatus status)
        => TryParseName(value, out status) && status is UserStatus.Active or UserStatus.Suspended;

    public static bool TryParseStatus(string? value, out UserStatus status)
        => TryParseName(value, out status);

    // Enum.TryParse also accepts numbers such as "2"; only the names are part of the API.
    private static bool TryParseName<TEnum>(string? value, out TEnum result)
        where TEnum : struct, Enum
    {
        result = default;

        var name = Enum.GetNames<TEnum>().FirstOrDefault(name =>
            string.Equals(name, value?.Trim(), StringComparison.OrdinalIgnoreCase));

        return name is not null && Enum.TryParse(name, out result);
    }
}
