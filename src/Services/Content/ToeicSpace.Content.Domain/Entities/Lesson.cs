using ToeicSpace.Content.Domain.Enums;
using ToeicSpace.Content.Domain.Exceptions;

namespace ToeicSpace.Content.Domain.Entities;

public sealed class Lesson
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ModuleId { get; set; }
    public CourseModule Module { get; set; } = null!;
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? TextContent { get; set; }
    public int? DurationSeconds { get; set; }
    public int SortOrder { get; set; }
    public bool IsFreePreview { get; set; }
    public LessonStatus Status { get; set; } = LessonStatus.Draft;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public List<LessonResource> Resources { get; set; } = [];
    public List<LessonVideo> Videos { get; set; } = [];

    public void SetMetadata(string title, string? description, string? textContent,
        int? durationSeconds, bool isFreePreview, DateTime now)
    {
        if (string.IsNullOrWhiteSpace(title) || title.Length > 200 || description?.Length > 2000 ||
            textContent?.Length > 100000 || durationSeconds is <= 0 or > 86400)
            throw new ContentRuleException("invalid_lesson", "Lesson metadata is invalid.");
        Title = title.Trim();
        Description = description?.Trim();
        TextContent = textContent;
        DurationSeconds = durationSeconds;
        IsFreePreview = isFreePreview;
        UpdatedAt = now;
    }

    public void Publish(DateTime now)
    {
        if (Status != LessonStatus.Draft) throw ContentRuleException.InvalidState();
        if (string.IsNullOrWhiteSpace(TextContent) && Videos.Count == 0 && Resources.Count == 0)
            throw new ContentRuleException("publish_requirements", "Lesson needs text, video or attachments to publish.", 409);
        Status = LessonStatus.Published;
        UpdatedAt = now;
    }

    public void Unpublish(DateTime now)
    {
        if (Status != LessonStatus.Published) throw ContentRuleException.InvalidState();
        Status = LessonStatus.Draft;
        UpdatedAt = now;
    }
}
