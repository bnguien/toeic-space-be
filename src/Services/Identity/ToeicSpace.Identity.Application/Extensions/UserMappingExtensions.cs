using ToeicSpace.Identity.Application.Features.Profile.Results;
using ToeicSpace.Identity.Application.Features.UserManagement.Results;
using ToeicSpace.Identity.Application.Models;
using ToeicSpace.Identity.Domain.Entities;
using ToeicSpace.Identity.Domain.Enums;

namespace ToeicSpace.Identity.Application.Extensions;

public static class UserMappingExtensions
{
    public static AuthenticatedUser ToAuthenticatedUser(this User user)
        => new(
            user.Id,
            user.FullName,
            user.Email,
            user.Role.ToString());

    /// <summary>
    /// Only verified, active and not deleted accounts may hold a session.
    /// </summary>
    public static bool CanSignIn(this User user)
        => user.DeletedAt is null
            && user.EmailVerifiedAt is not null
            && user.Status == UserStatus.Active;

    /// <summary>
    /// An administrator who can sign in. The last one must never be demoted, suspended or deleted.
    /// </summary>
    public static bool IsEffectiveAdministrator(this User user)
        => user.Role == UserRole.Admin && user.CanSignIn();

    public static ManagedUserDetailResult ToManagedUserDetailResult(
        this User user,
        int activeSessionCount,
        DateTime? lastActiveAt)
        => new(
            user.Id,
            user.FullName,
            user.Email,
            user.Phone,
            user.AvatarUrl,
            user.Role.ToString(),
            user.Status.ToString(),
            user.EmailVerifiedAt,
            user.CreatedAt,
            user.DeletedAt,
            user.DateOfBirth,
            user.Gender?.ToString(),
            user.Biography,
            user.TargetScore,
            user.CurrentLevel?.ToString(),
            activeSessionCount,
            lastActiveAt);

    public static UserProfileResult ToUserProfileResults(this User user)
        => new(
            user.Id,
            user.FullName,
            user.Email,
            user.Phone,
            user.AvatarUrl,
            user.DateOfBirth,
            user.Gender,
            user.Biography,
            user.TargetScore,
            user.CurrentLevel
        );
}
