namespace ToeicSpace.Assessment.Application.Features.Attempts.Commands.CheckAnswers;

public sealed class CheckAnswersValidator : AbstractValidator<CheckAnswersCommand>
{
    public const int MaximumQuestions = 60;

    public CheckAnswersValidator()
    {
        RuleFor(command => command.AttemptId).NotEmpty();
        RuleFor(command => command.QuestionIds)
            .NotEmpty()
            .Must(ids => ids.Count <= MaximumQuestions)
            .WithMessage($"Check at most {MaximumQuestions} questions at once.");
        RuleForEach(command => command.QuestionIds).NotEmpty();
    }
}
