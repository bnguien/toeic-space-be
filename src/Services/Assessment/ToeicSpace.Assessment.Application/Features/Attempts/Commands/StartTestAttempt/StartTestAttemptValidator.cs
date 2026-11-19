namespace ToeicSpace.Assessment.Application.Features.Attempts.Commands.StartTestAttempt;

public sealed class StartTestAttemptValidator : AbstractValidator<StartTestAttemptCommand>
{
    public StartTestAttemptValidator()
    {
        RuleFor(command => command.TestId).NotEmpty();

        RuleFor(command => command.Mode)
            .NotNull()
            .Must(mode => mode is AttemptMode.FullTest or AttemptMode.Practice)
            .WithMessage("Mode must be FullTest or Practice.");

        When(command => command.Mode == AttemptMode.Practice, () =>
        {
            RuleFor(command => command.Parts)
                .NotEmpty()
                .WithMessage("Choose at least one part to practice.");

            RuleForEach(command => command.Parts)
                .InclusiveBetween(1, 7);

            RuleFor(command => command.Parts)
                .Must(parts => parts is null || parts.Distinct().Count() == parts.Count)
                .WithMessage("Parts must not repeat.");
        });
    }
}
