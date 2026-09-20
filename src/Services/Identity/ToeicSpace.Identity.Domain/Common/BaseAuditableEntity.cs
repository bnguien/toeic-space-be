namespace ToeicSpace.Identity.Domain.Common;

public abstract class BaseAuditableEntity
{
    public Guid Id { get; set; }
    public DateTime CreatedAt { get; set; }
}