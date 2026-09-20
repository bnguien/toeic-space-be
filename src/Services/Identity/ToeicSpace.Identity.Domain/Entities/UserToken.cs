using ToeicSpace.Identity.Domain.Common;
using ToeicSpace.Identity.Domain.Enums;

namespace ToeicSpace.Identity.Domain.Entities;

public class UserToken : BaseAuditableEntity
{
    public Guid UserId { get; set; }

    public string TokenHash { get; set; } = string.Empty;

    public TokenType Type { get; set; }

    public DateTime ExpiresAt { get; set; }

    public bool IsRevoked { get; set; }

    public DateTime? UsedAt { get; set; }

    public User User { get; set; } = null!;
}