using System.Text.Json.Serialization;
using ToeicSpace.Content.Application.DTOs.Lessons;

namespace ToeicSpace.Content.Application.Features.Lessons.Commands.ReorderLessons;

public sealed record ReorderLessonsCommand(IReadOnlyList<Guid> LessonIds) : IRequest<IReadOnlyList<LessonSummaryDto>>
{
    [JsonIgnore] public Guid CourseId { get; set; }
    [JsonIgnore] public Guid ModuleId { get; set; }
}
