namespace ToeicSpace.Content.Application.Features.VocabularyQuizzes.Commands.CreateQuiz;

public sealed class CreateQuizValidator : AbstractValidator<CreateQuizCommand>
{
    public CreateQuizValidator()
    {
        RuleFor(x => x.WordIds).NotNull().WithMessage("wordIds is required.");
        RuleFor(x => x.WordIds.Count).InclusiveBetween(1, CreateQuizCommand.MaxWords)
            .WithMessage($"wordIds must contain 1 to {CreateQuizCommand.MaxWords} ids.")
            .When(x => x.WordIds is not null);
        RuleForEach(x => x.WordIds).NotEmpty().When(x => x.WordIds is not null);
    }
}
