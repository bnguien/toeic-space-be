namespace ToeicSpace.Assessment.Application.Dtos;

public sealed record TestDto(
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
    DateTime CreatedAt);

public sealed record TestPartSummaryDto(
    ToeicPart Part,
    int QuestionCount,
    int ExpectedQuestionCount);

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

public sealed record TestCategorySummaryDto(
    string Category,
    int TestCount,
    IReadOnlyList<int> Years);
