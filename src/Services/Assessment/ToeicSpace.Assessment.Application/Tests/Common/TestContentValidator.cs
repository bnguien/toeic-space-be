using ToeicSpace.Assessment.Application.Common.Validation;

namespace ToeicSpace.Assessment.Application.Tests.Common;

public sealed class TestContentValidator : AbstractValidator<ITestContent>
{
    public TestContentValidator()
    {
        RuleFor(test => test.Code)
            .MustBeCode();

        RuleFor(test => test.Title)
            .NotEmpty()
            .MaximumLength(255);

        RuleFor(test => test.Description)
            .MaximumLength(2000);

        RuleFor(test => test.Category)
            .MaximumLength(100);

        RuleFor(test => test.Year)
            .InclusiveBetween(2000, 2100);

        RuleFor(test => test.DurationMinutes)
            .InclusiveBetween(1, 300);

        RuleFor(test => test.AudioUrl)
            .MustBeOptionalHttpUrl();
    }

    public static void ApplyContent(
        ToeicTest test,
        ITestContent content)
    {
        test.Code = content.Code.Trim();
        test.Title = content.Title.Trim();
        test.Description = TextNormalizer.NullIfEmpty(content.Description);
        test.Category = TextNormalizer.NullIfEmpty(content.Category);
        test.Year = content.Year;
        test.DurationMinutes = content.DurationMinutes;
        test.AudioUrl = TextNormalizer.NullIfEmpty(content.AudioUrl);
        test.IsActive = content.IsActive;
    }
}
