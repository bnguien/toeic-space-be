using ToeicSpace.Assessment.Application.Common.Validation;
using ToeicSpace.Assessment.Application.DTOs.Questions;


namespace ToeicSpace.Assessment.Application.Features.Questions.Commands.UpdateQuestion;

/// <param name="ExpectedVersion">The version the client loaded; the update fails with 409 if it changed meanwhile.</param>
public sealed record UpdateQuestionCommand(
    Guid Id,
    int ExpectedVersion,
    Guid? TestId,
    Guid? PassageId,
    ToeicPart Part,
    int? QuestionNumber,
    string? QuestionText,
    string? AudioUrl,
    string? ImageUrl,
    string OptionA,
    string OptionB,
    string OptionC,
    string? OptionD,
    AnswerOption CorrectAnswer,
    string? Explanation,
    string? Transcript,
    QuestionDifficulty DifficultyLevel = QuestionDifficulty.Medium,
    string? Topic = null,
    int OrderIndex = 0,
    bool PreferAiExplanation = false) : IRequest<QuestionDetailDto>, IQuestionContent;
