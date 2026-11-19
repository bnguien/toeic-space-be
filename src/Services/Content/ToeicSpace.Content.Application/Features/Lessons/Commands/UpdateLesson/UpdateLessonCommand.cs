using System.Text.Json.Serialization;
using ToeicSpace.Content.Application.DTOs.Lessons;

namespace ToeicSpace.Content.Application.Features.Lessons.Commands.UpdateLesson;

public sealed record UpdateLessonCommand(string Title, string? Description, string? TextContent,
    IReadOnlyList<string>? VideoKeys, int? DurationSeconds, bool IsFreePreview) : IRequest<LessonDto>
{
    [JsonIgnore] public Guid CourseId { get; set; }
    [JsonIgnore] public Guid ModuleId { get; set; }
    [JsonIgnore] public Guid LessonId { get; set; }
    [JsonIgnore] public Guid AdminId { get; set; }
}
