namespace ToeicSpace.Assessment.Application.Questions.Commands.DeleteQuestion;

public sealed class DeleteQuestionValidator : AbstractValidator<DeleteQuestionCommand>
{
    public DeleteQuestionValidator()
    {
        RuleFor(command => command.Id)
            .NotEmpty();
    }
}
