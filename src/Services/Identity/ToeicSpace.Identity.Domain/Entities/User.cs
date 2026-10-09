using ToeicSpace.Identity.Domain.Common;
using ToeicSpace.Identity.Domain.Enums;

namespace ToeicSpace.Identity.Domain.Entities;

public class User : BaseAuditableEntity
{
    public string FullName { get; set; } = string.Empty;

    public string? Phone { get; set; }
    
    public string? AvatarUrl { get; set; }
    
    public DateOnly? DateOfBirth { get; set; }
    
    public UserGender? Gender { get; set; }
    
    public string? Biography { get; set; }
    
    public int? TargetScore { get; set; }
    
    public EnglishLevel? CurrentLevel { get; set; }

    public string Email { get; set; } = string.Empty;

    public string? PasswordHash { get; set; }

    public DateTime? EmailVerifiedAt { get; set; }

    public UserRole Role { get; set; }

    public UserStatus Status { get; set; }

    public DateTime? DeletedAt { get; set; }

    public ICollection<UserToken> Tokens { get; set; } = new List<UserToken>();

    public ICollection<UserOAuth> OAuthAccounts { get; set; } = new List<UserOAuth>();
}