using ToeicSpace.Identity.Domain.Enums;

namespace ToeicSpace.Identity.Application.Features.Profile.Results;

public sealed record UserProfileResult(
    Guid Id,
    string FullName,
    string Email,
    string? Phone,
    string? AvatarUrl,
    DateOnly? DateOfBirth,
    UserGender? Gender, 
    string? Biography,
    int? TargetScore,
    EnglishLevel? CurrentLevel
);