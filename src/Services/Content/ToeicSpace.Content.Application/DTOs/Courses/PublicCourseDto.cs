using ToeicSpace.Content.Domain.Enums;

namespace ToeicSpace.Content.Application.DTOs.Courses;

public sealed record PublicCourseSummaryDto(Guid Id, string Title, string Slug, string? ShortDescription,
    string? ThumbnailUrl, CourseExamType ExamType, CourseLevel Level, int? TargetScore);

public sealed record PublicLessonSummaryDto(Guid Id, string Title, string? Description,
    int? DurationSeconds, int SortOrder, bool IsFreePreview);

public sealed record PublicModuleDto(Guid Id, string Title, string? Description, int SortOrder,
    IReadOnlyList<PublicLessonSummaryDto> Lessons);

public sealed record PublicCourseDetailDto(Guid Id, string Title, string Slug, string? ThumbnailUrl,
    string? ShortDescription, string? Description, CourseExamType ExamType, CourseLevel Level,
    int? TargetScore, IReadOnlyList<string> Skills, IReadOnlyList<string> Objectives,
    IReadOnlyList<string> Prerequisites, IReadOnlyList<PublicModuleDto> Modules);

public sealed record PublicVideoDto(Guid Id, string FileName, string ContentType, long FileSize,
    int? DurationSeconds, int SortOrder, string Url);

public sealed record PublicAttachmentDto(Guid Id, string FileName, string ContentType, long FileSize, string Url);

public sealed record PublicLessonPreviewDto(Guid Id, string Title, string? TextContent,
    IReadOnlyList<PublicVideoDto> Videos, IReadOnlyList<PublicAttachmentDto> Attachments);
