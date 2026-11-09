using ToeicSpace.Content.Application.DTOs.Lessons;
using ToeicSpace.Content.Domain.Entities;

namespace ToeicSpace.Content.Application.Mappings;

public static class LessonMappingExtensions
{
    public static LessonSummaryDto ToSummary(this Lesson x) =>
        new(x.Id, x.Title, x.SortOrder, x.Status, x.IsFreePreview, x.DurationSeconds);
    public static LessonDto ToDto(this Lesson x) => new(x.Id, x.ModuleId, x.Title,
        x.Description, x.TextContent, x.DurationSeconds, x.SortOrder,
        x.IsFreePreview, x.Status, x.CreatedAt, x.UpdatedAt,
        x.Resources.OrderBy(r => r.CreatedAt).ThenBy(r => r.Id).Select(r => r.ToDto()).ToArray(),
        x.Videos.OrderBy(v => v.SortOrder).Select(v => v.ToDto()).ToArray());
    public static LessonResourceDto ToDto(this LessonResource x) =>
        new(x.Id, x.FileName, x.ObjectKey, x.ContentType, x.FileSize, x.CreatedAt);
    public static LessonVideoDto ToDto(this LessonVideo x) =>
        new(x.Id, x.FileName, x.ObjectKey, x.ContentType, x.FileSize, x.DurationSeconds, x.SortOrder, x.CreatedAt);
}
