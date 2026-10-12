namespace ToeicSpace.Identity.Application.Interfaces.Security;

/// <summary>
/// Temporarily blocks sign-in for an email address after repeated failures.
/// Unknown addresses are counted too, so the lockout does not reveal which accounts exist.
/// </summary>
public interface ILoginAttemptLimiter
{
    Task<bool> IsLockedOutAsync(
        string normalizedEmail,
        CancellationToken cancellationToken);

    Task RecordFailureAsync(
        string normalizedEmail,
        CancellationToken cancellationToken);

    Task ResetAsync(
        string normalizedEmail,
        CancellationToken cancellationToken);
}
