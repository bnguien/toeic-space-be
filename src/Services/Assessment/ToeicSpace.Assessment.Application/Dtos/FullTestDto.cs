namespace ToeicSpace.Assessment.Application.Dtos;

public sealed record FullTestDto(
    Guid Id,
    string Code,
    string Title,
    string? Description,
    string? Category,
    int? Year,
    int TotalQuestions,
    int DurationMinutes,
    int TotalListeningQuestions,
    int TotalReadingQuestions,
    string? AudioUrl,
    bool IncludesAnswers,
    IReadOnlyList<ContentPartDto> Parts);
