using ToeicSpace.Assessment.Application.Questions.Common;

namespace ToeicSpace.Assessment.Application.Questions.Commands.CreateQuestion;

public sealed class CreateQuestionValidator : AbstractValidator<CreateQuestionCommand>
{
    public CreateQuestionValidator()
    {
        Include(new QuestionContentValidator());
    }
}
