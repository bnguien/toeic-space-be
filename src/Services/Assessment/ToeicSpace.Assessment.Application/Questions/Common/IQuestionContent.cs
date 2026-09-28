namespace ToeicSpace.Assessment.Application.Questions.Common;

/// <summary>
/// Editable content shared by the create and update question commands.
/// </summary>
public interface IQuestionContent
{
    Guid? TestId { get; }

    Guid? PassageId { get; }

    ToeicPart Part { get; }

    int? QuestionNumber { get; }

    string? QuestionText { get; }

    string? AudioUrl { get; }

    string? ImageUrl { get; }

    string OptionA { get; }

    string OptionB { get; }

    string OptionC { get; }

    string? OptionD { get; }

    AnswerOption CorrectAnswer { get; }

    string? Explanation { get; }

    string? Transcript { get; }

    QuestionDifficulty DifficultyLevel { get; }

    string? Topic { get; }

    int OrderIndex { get; }

    bool PreferAiExplanation { get; }
}
