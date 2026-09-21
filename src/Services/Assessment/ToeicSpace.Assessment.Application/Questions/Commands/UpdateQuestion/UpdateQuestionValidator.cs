using ToeicSpace.Assessment.Application.Questions.Common;

namespace ToeicSpace.Assessment.Application.Questions.Commands.UpdateQuestion;

public sealed class UpdateQuestionValidator : AbstractValidator<UpdateQuestionCommand>
{
    public UpdateQuestionValidator()
    {
        RuleFor(command => command.Id)
            .NotEmpty();

        RuleFor(command => command.ExpectedVersion)
            .GreaterThan(0)
            .WithMessage("Expected version is required.");

        Include(new QuestionContentValidator());
    }
}
