using System.Text.Json.Serialization;
using ToeicSpace.Content.Application.DTOs.Lessons;

namespace ToeicSpace.Content.Application.Features.Lessons.Commands.AddLessonResource;

public sealed record AddLessonResourceCommand(string ObjectKey) : IRequest<LessonResourceDto>
{
    [JsonIgnore] public Guid CourseId { get; set; }
    [JsonIgnore] public Guid ModuleId { get; set; }
    [JsonIgnore] public Guid LessonId { get; set; }
    [JsonIgnore] public Guid AdminId { get; set; }
}
