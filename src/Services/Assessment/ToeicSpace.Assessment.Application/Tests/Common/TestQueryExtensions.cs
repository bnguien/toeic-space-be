namespace ToeicSpace.Assessment.Application.Tests.Common;

public static class TestQueryExtensions
{
    /// <summary>
    /// Learners only see published tests; content managers see every status.
    /// </summary>
    public static IQueryable<ToeicTest> VisibleTo(
        this IQueryable<ToeicTest> query,
        bool canManageContent)
    {
        return canManageContent
            ? query
            : query.Where(test => test.Status == ContentStatus.Active && test.IsActive);
    }

    public static async Task<(int PassageCount, IReadOnlyDictionary<ToeicPart, int> QuestionCounts)> LoadStatisticsAsync(
        this Data.IAssessmentDbContext context,
        Guid testId,
        CancellationToken cancellationToken)
    {
        var passageCount = await context.ToeicPassages
            .CountAsync(passage => passage.TestId == testId, cancellationToken);

        var questionCounts = await context.ToeicQuestions
            .Where(question => question.TestId == testId)
            .GroupBy(question => question.Part)
            .Select(group => new { Part = group.Key, Count = group.Count() })
            .ToDictionaryAsync(row => row.Part, row => row.Count, cancellationToken);

        return (passageCount, questionCounts);
    }
}
