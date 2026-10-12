using ToeicSpace.Identity.Application.Features.Profile.Results;
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
