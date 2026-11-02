namespace ToeicSpace.Assessment.Application.DTOs.Content;

/// <summary>
/// Question as rendered for taking a test or a practice set. Answer-revealing fields are null
/// when the viewer is not allowed to see the answer key.
/// </summary>
public sealed record ContentQuestionDto(
    Guid Id,
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
    AnswerOption? CorrectAnswer,
    string? Explanation,
    string? Transcript,
    string? TranscriptTranslation,
    QuestionDifficulty DifficultyLevel,
    int OrderIndex);
