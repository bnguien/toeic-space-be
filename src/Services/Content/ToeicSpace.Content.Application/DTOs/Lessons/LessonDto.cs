using ToeicSpace.Content.Domain.Enums;

namespace ToeicSpace.Content.Application.DTOs.Lessons;

public sealed record LessonResourceDto(Guid Id, string FileName, string ObjectKey,
    string ContentType, long FileSize, DateTime CreatedAt);
public sealed record LessonVideoDto(Guid Id, string FileName, string ObjectKey, string ContentType,
    long FileSize, int? DurationSeconds, int SortOrder, DateTime CreatedAt);
public sealed record LessonSummaryDto(Guid Id, string Title, int SortOrder, LessonStatus Status,
    bool IsFreePreview, int? DurationSeconds);
public sealed record LessonDto(Guid Id, Guid ModuleId, string Title, string? Description,
    string? TextContent, int? DurationSeconds, int SortOrder,
    bool IsFreePreview, LessonStatus Status, DateTime CreatedAt, DateTime UpdatedAt,
    IReadOnlyList<LessonResourceDto> Resources, IReadOnlyList<LessonVideoDto> Videos);
public sealed record LessonPreviewDto(LessonDto Lesson, IReadOnlyList<LessonVideoPreviewDto> Videos,
    IReadOnlyList<LessonResourcePreviewDto> Attachments);
public sealed record LessonResourcePreviewDto(Guid ResourceId, string FileName, string DownloadUrl);
public sealed record LessonVideoPreviewDto(Guid VideoId, string FileName, string StreamUrl, int SortOrder);
public sealed record AddLessonResourcesResult(int TotalCount, int AddedCount, int FailedCount,
    IReadOnlyList<AddLessonResourceItemResult> Items);
public sealed record AddLessonResourceItemResult(string ObjectKey, LessonResourceDto? Resource,
    string? Code, string? Message);
