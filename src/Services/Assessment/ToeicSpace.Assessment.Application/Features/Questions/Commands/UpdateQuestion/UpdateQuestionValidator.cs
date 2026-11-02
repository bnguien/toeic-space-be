using ToeicSpace.Assessment.Application.Common.Validation;


namespace ToeicSpace.Assessment.Application.Features.Questions.Commands.UpdateQuestion;

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
