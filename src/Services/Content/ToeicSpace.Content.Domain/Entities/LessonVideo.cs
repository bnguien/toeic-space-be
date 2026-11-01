namespace ToeicSpace.Content.Domain.Entities;

public sealed class LessonVideo
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid LessonId { get; set; }
    public Lesson Lesson { get; set; } = null!;
    public string FileName { get; set; } = string.Empty;
    public string ObjectKey { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long FileSize { get; set; }
    public int? DurationSeconds { get; set; }
    public int SortOrder { get; set; }
    public DateTime CreatedAt { get; set; }
}
