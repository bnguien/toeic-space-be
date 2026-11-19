namespace ToeicSpace.Assessment.Application.Features.Questions.Commands.DeleteQuestion;

public sealed class DeleteQuestionValidator : AbstractValidator<DeleteQuestionCommand>
{
    public DeleteQuestionValidator()
    {
        RuleFor(command => command.Id)
            .NotEmpty();
    }
}
