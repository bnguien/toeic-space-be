using System.Text.Json.Serialization;
using ToeicSpace.Content.Application.DTOs.Lessons;

namespace ToeicSpace.Content.Application.Features.Lessons.Commands.SetLessonFreePreview;

public sealed record SetLessonFreePreviewCommand(bool IsFreePreview) : IRequest<LessonDto>
{
    [JsonIgnore] public Guid CourseId { get; set; }
    [JsonIgnore] public Guid ModuleId { get; set; }
    [JsonIgnore] public Guid LessonId { get; set; }
}
