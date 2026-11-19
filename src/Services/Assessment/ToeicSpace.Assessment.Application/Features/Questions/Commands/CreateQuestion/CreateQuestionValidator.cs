using ToeicSpace.Assessment.Application.Common.Validation;


namespace ToeicSpace.Assessment.Application.Features.Questions.Commands.CreateQuestion;

public sealed class CreateQuestionValidator : AbstractValidator<CreateQuestionCommand>
{
    public CreateQuestionValidator()
    {
        Include(new QuestionContentValidator());
    }
}
