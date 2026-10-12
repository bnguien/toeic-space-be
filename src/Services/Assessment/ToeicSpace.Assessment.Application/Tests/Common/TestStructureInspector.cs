using ToeicSpace.Assessment.Application.Data;
using ToeicSpace.Assessment.Domain.Rules;

namespace ToeicSpace.Assessment.Application.Tests.Common;

/// <summary>
/// Verifies that a test matches the standard ETS structure before it can be published.
/// </summary>
public sealed class TestStructureInspector
{
    private readonly IAssessmentDbContext _context;

    public TestStructureInspector(IAssessmentDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<string>> FindProblemsAsync(
        Guid testId,
        CancellationToken cancellationToken)
    {
        var questions = await _context.ToeicQuestions
            .AsNoTracking()
            .Where(question => question.TestId == testId)
            .Select(question => new
            {
                question.Part,
                question.QuestionNumber,
                question.PassageId,
                question.Status
            })
            .ToListAsync(cancellationToken);

        var problems = new List<string>();

        if (questions.Count != ToeicPartRules.StandardTestQuestionCount)
        {
            problems.Add($"The test has {questions.Count} questions instead of {ToeicPartRules.StandardTestQuestionCount}.");
        }

        foreach (var part in Enum.GetValues<ToeicPart>())
        {
            var count = questions.Count(question => question.Part == part);
            var expected = ToeicPartRules.GetStandardQuestionCount(part);

            if (count != expected)
            {
                problems.Add($"Part {(int)part} has {count} questions instead of {expected}.");
            }
        }

        var duplicateNumbers = questions
            .Where(question => question.QuestionNumber.HasValue)
            .GroupBy(question => question.QuestionNumber!.Value)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .Order()
            .ToList();

        if (duplicateNumbers.Count > 0)
        {
            problems.Add($"Duplicate question numbers: {string.Join(", ", duplicateNumbers)}.");
        }

        var outOfRange = questions.Count(question =>
            !question.QuestionNumber.HasValue
            || !ToeicPartRules.IsInStandardQuestionNumberRange(question.Part, question.QuestionNumber.Value));

        if (outOfRange > 0)
        {
            problems.Add($"{outOfRange} questions have a number outside the standard range of their part.");
        }

        var missingPassage = questions.Count(question =>
            ToeicPartRules.RequiresPassage(question.Part) && !question.PassageId.HasValue);

        if (missingPassage > 0)
        {
            problems.Add($"{missingPassage} Part 3/4/6/7 questions are not linked to a passage.");
        }

        var inactive = questions.Count(question => question.Status != ContentStatus.Active);

        if (inactive > 0)
        {
            problems.Add($"{inactive} questions are not Active.");
        }

        return problems;
    }
}
