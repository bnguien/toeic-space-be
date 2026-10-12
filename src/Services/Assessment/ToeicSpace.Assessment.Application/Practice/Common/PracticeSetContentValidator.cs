using ToeicSpace.Assessment.Application.Common.Validation;

namespace ToeicSpace.Assessment.Application.Practice.Common;

public sealed class PracticeSetContentValidator : AbstractValidator<IPracticeSetContent>
{
    public PracticeSetContentValidator()
    {
        RuleFor(practiceSet => practiceSet.Code)
            .MustBeCode();

        RuleFor(practiceSet => practiceSet.Title)
            .NotEmpty()
            .MaximumLength(255);

        RuleFor(practiceSet => practiceSet.Description)
            .MaximumLength(2000);

        RuleFor(practiceSet => practiceSet.Kind)
            .IsInEnum();

        RuleFor(practiceSet => practiceSet.Part)
            .IsInEnum();

        RuleFor(practiceSet => practiceSet.Level)
            .NotNull()
            .InclusiveBetween(1, 5)
            .When(practiceSet => practiceSet.Kind == PracticeSetKind.Level)
            .WithMessage("Level practice sets need a level between 1 and 5.");

        RuleFor(practiceSet => practiceSet.Level)
            .Null()
            .When(practiceSet => practiceSet.Kind != PracticeSetKind.Level)
            .WithMessage("Only level practice sets have a level.");

        RuleFor(practiceSet => practiceSet.TargetScore)
            .InclusiveBetween(10, 990);

        RuleFor(practiceSet => practiceSet.DurationMinutes)
            .InclusiveBetween(1, 1000);

        RuleFor(practiceSet => practiceSet.OrderIndex)
            .GreaterThanOrEqualTo(0);
    }

    public static void ApplyContent(
        ToeicPracticeSet practiceSet,
        IPracticeSetContent content)
    {
        practiceSet.Code = content.Code.Trim();
        practiceSet.Title = content.Title.Trim();
        practiceSet.Description = TextNormalizer.NullIfEmpty(content.Description);
        practiceSet.Kind = content.Kind;
        practiceSet.Part = content.Part;
        practiceSet.Level = content.Level;
        practiceSet.TargetScore = content.TargetScore;
        practiceSet.DurationMinutes = content.DurationMinutes;
        practiceSet.OrderIndex = content.OrderIndex;
    }
}
