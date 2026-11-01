using ToeicSpace.Content.Domain.Enums;

namespace ToeicSpace.Content.Domain.Entities;

public sealed class LessonUpload
{
    public string Key { get; set; } = string.Empty;
    public Guid RequestedBy { get; set; }
    public LessonUploadKind Kind { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public string? FileSignature { get; set; }
    public long SizeBytes { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
    public DateTime? ConfirmedAt { get; set; }
}
