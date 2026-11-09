using ToeicSpace.Assessment.Application.Common.Validation;
using ToeicSpace.Assessment.Application.Features.PracticeSets.Commands.ReplacePracticeSetItems;


namespace ToeicSpace.Assessment.Application.Features.PracticeSets.Commands.CreatePracticeSet;

public sealed class CreatePracticeSetValidator : AbstractValidator<CreatePracticeSetCommand>
{
    public CreatePracticeSetValidator()
    {
        Include(new PracticeSetContentValidator());
        RuleFor(command => command.Status)
            .Must(status => status is ContentStatus.Draft or ContentStatus.Active);
        RuleFor(command => command.QuestionIds)
            .Must(ids => ids is null || ids.Count <= ReplacePracticeSetItemsValidator.MaxQuestions)
            .WithMessage($"A practice set can have at most {ReplacePracticeSetItemsValidator.MaxQuestions} questions.")
            .Must(ids => ids is null || ids.Distinct().Count() == ids.Count)
            .WithMessage("A question can only appear once in a practice set.")
            .Must(ids => ids is null || !ids.Contains(Guid.Empty))
            .WithMessage("Question IDs must not be empty.");
    }
}
