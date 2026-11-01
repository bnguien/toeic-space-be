using System.Text.Json.Serialization;
using ToeicSpace.Content.Application.DTOs.Lessons;

namespace ToeicSpace.Content.Application.Features.Lessons.Commands.CreateLesson;

public sealed record CreateLessonCommand(string Title, string? Description, string? TextContent,
    IReadOnlyList<string>? VideoKeys, int? DurationSeconds, int? SortOrder, bool IsFreePreview = false) : IRequest<LessonDto>
{
    [JsonIgnore] public Guid CourseId { get; set; }
    [JsonIgnore] public Guid ModuleId { get; set; }
    [JsonIgnore] public Guid AdminId { get; set; }
}
