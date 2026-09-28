using ToeicSpace.Assessment.Application.Common.Validation;
using ToeicSpace.Assessment.Application.Data;

namespace ToeicSpace.Assessment.Application.Questions.Common;

/// <summary>
/// Database-backed checks that a question fits into its test and passage.
/// </summary>
public sealed class QuestionReferenceGuard
{
    private readonly IAssessmentDbContext _context;

    public QuestionReferenceGuard(IAssessmentDbContext context)
    {
        _context = context;
    }

    public async Task EnsureValidAsync(
        IQuestionContent content,
        Guid? currentQuestionId,
        CancellationToken cancellationToken)
    {
        if (content.TestId is { } testId)
        {
            var testExists = await _context.ToeicTests
                .AnyAsync(test => test.Id == testId, cancellationToken);

            if (!testExists)
            {
                throw AppException.NotFound(nameof(ToeicTest), testId);
            }

            var numberTaken = await _context.ToeicQuestions
                .AnyAsync(
                    question => question.TestId == testId
                        && question.QuestionNumber == content.QuestionNumber
                        && question.Id != currentQuestionId,
                    cancellationToken);

            if (numberTaken)
            {
                throw AppException.Conflict(
                    $"Question number {content.QuestionNumber} already exists in this test.",
                    ErrorCodes.DuplicateQuestionNumber);
            }
        }

        if (content.PassageId is { } passageId)
        {
            var passage = await _context.ToeicPassages
                .AsNoTracking()
                .Where(item => item.Id == passageId)
                .Select(item => new { item.TestId, item.Part })
                .FirstOrDefaultAsync(cancellationToken)
                ?? throw AppException.NotFound(nameof(ToeicPassage), passageId);

            if (passage.TestId != content.TestId)
            {
                throw AppException.Validation(
                    "The passage belongs to a different test than the question.",
                    ErrorCodes.InvalidReference);
            }

            if (passage.Part != content.Part)
            {
                throw AppException.Validation(
                    $"The passage is for Part {(int)passage.Part} but the question is for Part {(int)content.Part}.",
                    ErrorCodes.InvalidReference);
            }
        }
    }

    public static void ApplyContent(
        ToeicQuestion question,
        IQuestionContent content)
    {
        question.TestId = content.TestId;
        question.PassageId = content.PassageId;
        question.Part = content.Part;
        question.QuestionNumber = content.QuestionNumber;
        question.QuestionText = TextNormalizer.NullIfEmpty(content.QuestionText);
        question.AudioUrl = TextNormalizer.NullIfEmpty(content.AudioUrl);
        question.ImageUrl = TextNormalizer.NullIfEmpty(content.ImageUrl);
        question.OptionA = content.OptionA.Trim();
        question.OptionB = content.OptionB.Trim();
        question.OptionC = content.OptionC.Trim();
        question.OptionD = TextNormalizer.NullIfEmpty(content.OptionD);
        question.CorrectAnswer = content.CorrectAnswer;
        question.Explanation = TextNormalizer.NullIfEmpty(content.Explanation);
        question.Transcript = TextNormalizer.NullIfEmpty(content.Transcript);
        question.DifficultyLevel = content.DifficultyLevel;
        question.Topic = TextNormalizer.NullIfEmpty(content.Topic);
        question.OrderIndex = content.OrderIndex;
        question.PreferAiExplanation = content.PreferAiExplanation;
    }
}
