namespace ToeicSpace.Assessment.Application.Models.Attempts;

/// <summary>
/// Private, immutable copy of a source question (including the answer key) stored per attempt.
/// </summary>
public sealed record QuestionSnapshot(
    Guid Id,
    Guid? PassageId,
    ToeicPart Part,
    int Version,
    int? QuestionNumber,
    string? QuestionText,
    string OptionA,
    string OptionB,
    string OptionC,
    string? OptionD,
    string? AudioUrl,
    string? ImageUrl,
    string? Transcript,
    AnswerOption CorrectAnswer,
    string? Explanation,
    QuestionDifficulty Difficulty,
    string? Topic);
