namespace ToeicSpace.Identity.Application.DTOs.Users;

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
