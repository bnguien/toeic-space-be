using ToeicSpace.Assessment.Application.Dtos;
using ToeicSpace.Assessment.Application.Questions.Common;

namespace ToeicSpace.Assessment.Application.Questions.Commands.CreateQuestion;

public sealed record CreateQuestionCommand(
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
