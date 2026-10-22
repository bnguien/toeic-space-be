namespace ToeicSpace.Identity.Application.Features.UserManagement.Results;

/// <summary>
/// Row of the administrator user list. Role and Status are enum names,
/// "Inactive" meaning the email address has not been verified yet.
/// </summary>
public sealed record ManagedUserResult(
    Guid Id,
    string FullName,
    string Email,
    string? Phone,
    string? AvatarUrl,
    string Role,
    string Status,
    bool EmailVerified,
    DateTime CreatedAt,
    DateTime? DeletedAt,
    DateTime? LastActiveAt);

public sealed record ManagedUserDetailResult(
    Guid Id,
    string FullName,
    string Email,
    string? Phone,
    string? AvatarUrl,
    string Role,
    string Status,
    DateTime? EmailVerifiedAt,
    DateTime CreatedAt,
    DateTime? DeletedAt,
    DateOnly? DateOfBirth,
    string? Gender,
    string? Biography,
    int? TargetScore,
    string? CurrentLevel,
    int ActiveSessionCount,
    DateTime? LastActiveAt);

/// <summary>
/// The temporary password is only returned here; it is never stored in plain text.
/// </summary>
public sealed record CreateUserResult(
    ManagedUserDetailResult User,
    string TemporaryPassword);

/// <summary>
/// Counts for the list header. Every count except Deleted excludes soft-deleted users.
/// Trend covers the requested period in the caller's time zone.
/// </summary>
public sealed record UserStatisticsResult(
    int Total,
    int Active,
    int Suspended,
    int Unverified,
    int Deleted,
    int CreatedLast30Days,
    UserTrendResult Trend);

/// <param name="Bucket">"hour" or "day": the size of each bucket is BucketHours.</param>
/// <param name="Registrations">Accounts created in the period (deleted ones included).</param>
/// <param name="ActiveUsers">Distinct users who signed in or refreshed a session in the period.</param>
/// <param name="PreviousRegistrations">Same figure for the period just before, for comparison.</param>
public sealed record UserTrendResult(
    string Period,
    string Bucket,
    int BucketHours,
    IReadOnlyList<TrendBucketResult> Buckets,
    int Registrations,
    int PreviousRegistrations,
    int ActiveUsers,
    int PreviousActiveUsers);

/// <param name="Start">Local start time of the bucket (no offset), e.g. 2026-10-22T13:00:00.</param>
public sealed record TrendBucketResult(DateTime Start, int Registrations, int ActiveUsers);
