using ToeicSpace.Content.Application.DTOs.Courses;
using ToeicSpace.Content.Domain.Enums;

namespace ToeicSpace.Content.Application.Features.Courses.Commands.CreateCourse;

public sealed record CreateCourseCommand(string Code, string Title, string Slug, string? ShortDescription,
    string? Description, string? ThumbnailKey, CourseExamType ExamType, CourseLevel Level, int? TargetScore,
    List<string>? Skills, List<string>? Objectives, List<string>? Prerequisites,
    [property: System.Text.Json.Serialization.JsonIgnore] Guid AdminId = default) : IRequest<CourseDto>;
