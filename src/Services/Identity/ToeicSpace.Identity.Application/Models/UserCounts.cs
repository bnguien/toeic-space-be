namespace ToeicSpace.Identity.Application.Models;

/// <summary>
/// Every count except Deleted excludes soft-deleted users.
/// </summary>
public sealed record UserCounts(
    int Total,
    int Active,
    int Suspended,
    int Unverified,
    int Deleted,
    int CreatedSince);
