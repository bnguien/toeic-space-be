using System.Text.Json.Serialization;
using ToeicSpace.Identity.Application.Features.Profile.Results;
using ToeicSpace.Identity.Domain.Enums;

namespace ToeicSpace.Identity.Application.Features.Profile.Commands;

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