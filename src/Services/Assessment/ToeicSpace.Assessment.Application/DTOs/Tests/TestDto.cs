namespace ToeicSpace.Assessment.Application.DTOs.Tests;

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
