namespace ToeicSpace.Content.Domain.Entities;

public sealed class ThumbnailUpload
{
    public string Key { get; set; } = string.Empty;
    public Guid RequestedBy { get; set; }
    public string ContentType { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
    public DateTime? ConfirmedAt { get; set; }
}
