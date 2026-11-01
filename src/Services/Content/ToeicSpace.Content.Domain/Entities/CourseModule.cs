using ToeicSpace.Content.Domain.Exceptions;

namespace ToeicSpace.Content.Domain.Entities;

public sealed class CourseModule
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid CourseId { get; set; }
    public Course Course { get; set; } = null!;
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int SortOrder { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public List<Lesson> Lessons { get; set; } = [];

    public void SetMetadata(string title, string? description, DateTime now)
    {
        if (string.IsNullOrWhiteSpace(title) || title.Length > 200 || description?.Length > 2000)
            throw new ContentRuleException("invalid_module", "Module title is required (max 200), description max 2000.");
        Title = title.Trim();
        Description = description?.Trim();
        UpdatedAt = now;
    }
}
