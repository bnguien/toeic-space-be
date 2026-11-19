namespace ToeicSpace.Assessment.Application.Features.Attempts.Commands.SaveAnswer;

public sealed class SaveAnswerValidator : AbstractValidator<SaveAnswerCommand>
{
    public const int MaximumTimeSpentSeconds = 3600;

    public SaveAnswerValidator()
    {
        RuleFor(command => command.AttemptId).NotEmpty();
        RuleFor(command => command.QuestionId).NotEmpty();
        RuleFor(command => command.MutationId).NotEmpty();
        RuleFor(command => command.ExpectedRevision).GreaterThanOrEqualTo(0);
        RuleFor(command => command.Answer!.Value).IsInEnum().When(command => command.Answer.HasValue);
        RuleFor(command => command.TimeSpentSeconds)
            .InclusiveBetween(0, MaximumTimeSpentSeconds)
            .When(command => command.TimeSpentSeconds.HasValue);
    }
}
