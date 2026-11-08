namespace ToeicSpace.Content.Application.Features.GrammarAttempts.Commands.RetryAttemptAnswer;

public sealed class RetryAttemptAnswerValidator : AbstractValidator<RetryAttemptAnswerCommand>
{
    public RetryAttemptAnswerValidator()
    {
        RuleFor(x => x.QuestionId).NotEmpty().Matches("^[A-Za-z0-9_.-]{1,40}$").WithMessage("Invalid questionId.");
    }
}
