using System.Text.Json.Serialization;
using ToeicSpace.Content.Application.DTOs.Courses;
using ToeicSpace.Content.Domain.Enums;

namespace ToeicSpace.Content.Application.Features.Courses.Commands.UpdateCourse;

public sealed record UpdateCourseCommand(string? Code, string Title, string Slug, string? ShortDescription,
    string? Description, string? ThumbnailKey, CourseExamType ExamType, CourseLevel Level, int? TargetScore,
    List<string>? Skills, List<string>? Objectives, List<string>? Prerequisites) : IRequest<CourseDto>
{
    [JsonIgnore] public Guid CourseId { get; set; }
    [JsonIgnore] public Guid AdminId { get; set; }
}
