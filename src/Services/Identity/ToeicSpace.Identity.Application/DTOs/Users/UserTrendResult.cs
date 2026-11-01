namespace ToeicSpace.Identity.Application.DTOs.Users;

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
