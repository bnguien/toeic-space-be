namespace ToeicSpace.Assessment.Application.DTOs.Questions;

public sealed record QuestionDto(
    Guid Id,
    Guid? TestId,
    Guid? PassageId,
    ToeicPart Part,
    ToeicSection Section,
    int? QuestionNumber,
    string? QuestionText,
    string? AudioUrl,
    string? ImageUrl,
    string OptionA,
    string OptionB,
    string OptionC,
    string? OptionD,
    QuestionDifficulty DifficultyLevel,
    string? Topic,
    ContentStatus Status,
    int Version,
    int OrderIndex);
