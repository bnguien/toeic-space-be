using ToeicSpace.Content.Application.DTOs.Lessons;

namespace ToeicSpace.Content.Application.DTOs.Modules;

public sealed record ModuleDto(Guid Id, Guid CourseId, string Title, string? Description,
    int SortOrder, DateTime CreatedAt, DateTime UpdatedAt,
    IReadOnlyList<LessonSummaryDto>? Lessons = null);

public sealed record ModuleSummaryDto(Guid Id, string Title, int SortOrder);
