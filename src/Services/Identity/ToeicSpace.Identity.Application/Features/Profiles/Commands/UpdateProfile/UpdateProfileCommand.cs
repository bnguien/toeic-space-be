using System.Text.Json.Serialization;
using ToeicSpace.Identity.Application.DTOs.Profiles;
using ToeicSpace.Identity.Domain.Enums;

namespace ToeicSpace.Identity.Application.Features.Profiles.Commands.UpdateProfile;

public sealed record UpdateProfileCommand(
    string FullName,
    string? Phone, 
    string? AvatarUrl,
    DateOnly? DateOfBirth,
    UserGender? Gender,
    string? Biography,
    int? TargetScore,
    EnglishLevel? CurrentLevel
) : IRequest<UserProfileResult>
{
    [JsonIgnore]
    public Guid UserId { get; set; }
}