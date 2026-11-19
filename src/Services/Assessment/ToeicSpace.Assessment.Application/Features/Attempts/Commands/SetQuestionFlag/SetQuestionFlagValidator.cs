namespace ToeicSpace.Assessment.Application.Features.Attempts.Commands.SetQuestionFlag;

public sealed class SetQuestionFlagValidator : AbstractValidator<SetQuestionFlagCommand>
{
    public SetQuestionFlagValidator()
    {
        RuleFor(command => command.AttemptId).NotEmpty();
        RuleFor(command => command.QuestionId).NotEmpty();
    }
}
