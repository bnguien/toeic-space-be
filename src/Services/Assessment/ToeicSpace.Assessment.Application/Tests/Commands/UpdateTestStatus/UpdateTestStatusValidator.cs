namespace ToeicSpace.Assessment.Application.Tests.Commands.UpdateTestStatus;

public sealed class UpdateTestStatusValidator : AbstractValidator<UpdateTestStatusCommand>
{
    public UpdateTestStatusValidator()
    {
        RuleFor(command => command.Id)
            .NotEmpty();

        RuleFor(command => command.Status)
            .IsInEnum();
    }
}
