using ToeicSpace.Assessment.Application.Common.Validation;
using ToeicSpace.Assessment.Domain.Rules;

namespace ToeicSpace.Assessment.Application.Questions.Common;

/// <summary>
/// ETS format rules for a question. Database checks (test/passage existence, duplicates)
/// are done by <see cref="QuestionReferenceGuard"/>.
/// </summary>
public sealed class QuestionContentValidator : AbstractValidator<IQuestionContent>
{
    public QuestionContentValidator()
    {
        RuleFor(question => question.Part)
            .IsInEnum();

        RuleFor(question => question.OptionA)
            .NotEmpty();

        RuleFor(question => question.OptionB)
            .NotEmpty();

        RuleFor(question => question.OptionC)
            .NotEmpty();

        RuleFor(question => question.OptionD)
            .NotEmpty()
            .When(question => ToeicPartRules.HasOptionD(question.Part))
            .WithMessage("Option D is required for this part.");

        RuleFor(question => question.OptionD)
            .Empty()
            .When(question => !ToeicPartRules.HasOptionD(question.Part))
            .WithMessage("Part 2 questions only have options A, B and C.");

        RuleFor(question => question.CorrectAnswer)
            .IsInEnum()
            .Must((question, answer) => ToeicPartRules.IsValidAnswer(question.Part, answer))
            .WithMessage(question => ToeicPartRules.HasOptionD(question.Part)
                ? "Correct answer must be A, B, C or D."
                : "Correct answer must be A, B or C for Part 2.");

        RuleFor(question => question.PassageId)
            .NotEmpty()
            .When(question => ToeicPartRules.RequiresPassage(question.Part))
            .WithMessage("Part 3, 4, 6 and 7 questions must belong to a passage.");

        RuleFor(question => question.PassageId)
            .Empty()
            .When(question => !ToeicPartRules.AllowsPassage(question.Part))
            .WithMessage("Part 1, 2 and 5 questions are standalone and cannot belong to a passage.");

        RuleFor(question => question.ImageUrl)
            .NotEmpty()
            .When(question => ToeicPartRules.RequiresImage(question.Part))
            .WithMessage("Part 1 questions must have a photo.");

        RuleFor(question => question.QuestionNumber)
            .NotNull()
            .When(question => question.TestId.HasValue)
            .WithMessage("Question number is required for questions of a test.");

        RuleFor(question => question.QuestionNumber)
            .Null()
            .When(question => !question.TestId.HasValue)
            .WithMessage("Question number is only used for questions of a test.");

        RuleFor(question => question.QuestionNumber)
            .Must((question, number) => ToeicPartRules.IsInStandardQuestionNumberRange(question.Part, number!.Value))
            .When(question => question.TestId.HasValue && question.QuestionNumber.HasValue && Enum.IsDefined(question.Part))
            .WithMessage(question =>
            {
                var (first, last) = ToeicPartRules.GetStandardQuestionNumberRange(question.Part);
                return $"Part {(int)question.Part} question numbers must be between {first} and {last}.";
            });

        RuleFor(question => question.AudioUrl)
            .MustBeOptionalHttpUrl();

        RuleFor(question => question.ImageUrl)
            .MustBeOptionalHttpUrls();

        RuleFor(question => question.DifficultyLevel)
            .IsInEnum();

        RuleFor(question => question.Topic)
            .MaximumLength(200);

        RuleFor(question => question.OrderIndex)
            .GreaterThanOrEqualTo(0);

        RuleFor(question => question.QuestionText).MustBeSafeRichText();
        RuleFor(question => question.OptionA).MustBeSafeRichText();
        RuleFor(question => question.OptionB).MustBeSafeRichText();
        RuleFor(question => question.OptionC).MustBeSafeRichText();
        RuleFor(question => question.OptionD).MustBeSafeRichText();
        RuleFor(question => question.Explanation).MustBeSafeRichText();
        RuleFor(question => question.Transcript).MustBeSafeRichText();
    }
}
