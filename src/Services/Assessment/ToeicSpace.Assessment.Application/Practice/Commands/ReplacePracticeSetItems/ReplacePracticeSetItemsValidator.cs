namespace ToeicSpace.Assessment.Application.Practice.Commands.ReplacePracticeSetItems;

public sealed class ReplacePracticeSetItemsValidator : AbstractValidator<ReplacePracticeSetItemsCommand>
{
    public const int MaxQuestions = 2000;

    public ReplacePracticeSetItemsValidator()
    {
        RuleFor(command => command.Id)
            .NotEmpty();

        RuleFor(command => command.QuestionIds)
            .Cascade(CascadeMode.Stop)
            .NotNull()
            .Must(questionIds => questionIds.Count <= MaxQuestions)
            .WithMessage($"A practice set can have at most {MaxQuestions} questions.")
            .Must(questionIds => questionIds.Distinct().Count() == questionIds.Count)
            .WithMessage("A question can only appear once in a practice set.");

        RuleForEach(command => command.QuestionIds)
            .NotEmpty();

        RuleFor(command => command.ExpectedQuestionIds)
            .Cascade(CascadeMode.Stop)
            .NotNull()
            .Must(ids => ids.Count <= MaxQuestions)
            .Must(ids => ids.Distinct().Count() == ids.Count);
        RuleForEach(command => command.ExpectedQuestionIds).NotEmpty();
    }
}
