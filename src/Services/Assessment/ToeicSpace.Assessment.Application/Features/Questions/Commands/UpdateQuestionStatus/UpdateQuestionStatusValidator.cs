namespace ToeicSpace.Assessment.Application.Features.Questions.Commands.UpdateQuestionStatus;

public sealed class UpdateQuestionStatusValidator : AbstractValidator<UpdateQuestionStatusCommand>
{
    public UpdateQuestionStatusValidator()
    {
        RuleFor(command => command.Id)
            .NotEmpty();

        RuleFor(command => command.Status)
            .IsInEnum();
    }
}
