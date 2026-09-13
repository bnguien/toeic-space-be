using ToeicSpace.Identity.Domain.Common;
using ToeicSpace.Identity.Domain.Enums;

namespace ToeicSpace.Identity.Domain.Entities;

public class UserOAuth : BaseAuditableEntity
{
    public Guid UserId { get; set; }

    public OAuthProvider Provider { get; set; }

    public string ProviderSubject { get; set; } = string.Empty;

    public string? EmailAtLink { get; set; }

    public DateTime? LastLoginAt { get; set; }

    public User User { get; set; } = null!;
}