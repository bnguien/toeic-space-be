namespace ToeicSpace.Assessment.Application.DTOs.Tests;

public sealed record TestDetailDto(
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
    bool IsActive,
    ContentStatus Status,
    int PassageCount,
    int QuestionCount,
    int ListeningQuestionCount,
    int ReadingQuestionCount,
    IReadOnlyList<TestPartSummaryDto> Parts,
    DateTime CreatedAt,
    DateTime? UpdatedAt);
