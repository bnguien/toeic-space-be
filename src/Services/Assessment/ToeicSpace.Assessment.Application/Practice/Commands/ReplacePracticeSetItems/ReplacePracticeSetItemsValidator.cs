namespace ToeicSpace.Assessment.Application.Practice.Commands.ReplacePracticeSetItems;

public sealed class ReplacePracticeSetItemsValidator : AbstractValidator<ReplacePracticeSetItemsCommand>
{
    public const int MaxQuestions = 2000;

    public ReplacePracticeSetItemsValidator()
    {
        RuleFor(command => command.Id)
            .NotEmpty();

        RuleFor(command => command.QuestionIds)
            .NotNull()
            .Must(questionIds => questionIds.Count <= MaxQuestions)
            .WithMessage($"A practice set can have at most {MaxQuestions} questions.")
            .Must(questionIds => questionIds.Distinct().Count() == questionIds.Count)
            .WithMessage("A question can only appear once in a practice set.");

        RuleForEach(command => command.QuestionIds)
            .NotEmpty();
    }
}
