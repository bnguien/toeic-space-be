using ToeicSpace.Content.Domain.Enums;
using ToeicSpace.Content.Application.DTOs.Modules;

namespace ToeicSpace.Content.Application.DTOs.Courses;

public sealed record CourseDto(Guid Id, string Code, string Title, string Slug, string? ShortDescription,
    string? Description, string? ThumbnailKey, CourseExamType ExamType, CourseLevel Level, int? TargetScore,
    IReadOnlyList<string> Skills, IReadOnlyList<string> Objectives, IReadOnlyList<string> Prerequisites,
    CourseStatus Status, DateTime CreatedAt, DateTime UpdatedAt, DateTime? PublishedAt,
    IReadOnlyList<ModuleSummaryDto>? Modules = null, decimal Price = 0, int? Capacity = null, string Currency = "VND");
