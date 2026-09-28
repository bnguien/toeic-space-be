using ToeicSpace.Assessment.Application.Data;

namespace ToeicSpace.Assessment.Application.Questions.Common;

/// <summary>
/// Prevents changes that would leave a published practice set short of questions. Learners never see
/// questions that are not Active, so archiving or deleting one silently shrinks the set.
/// </summary>
public static class PublishedPracticeSetGuard
{
    private const int MaxCodesInMessage = 5;

    public static async Task EnsureQuestionIsNotUsedByPublishedSetsAsync(
        IAssessmentDbContext context,
        Guid questionId,
        string action,
        CancellationToken cancellationToken)
    {
        var codes = await context.ToeicPracticeSetItems
            .AsNoTracking()
            .Where(item => item.QuestionId == questionId && item.PracticeSet.Status == ContentStatus.Active)
            .Select(item => item.PracticeSet.Code)
            .Distinct()
            .OrderBy(code => code)
            .Take(MaxCodesInMessage)
            .ToListAsync(cancellationToken);

        if (codes.Count > 0)
        {
            throw AppException.Conflict(
                $"Cannot {action} because it is used by published practice sets ({string.Join(", ", codes)}). "
                + "Remove it from those sets or move them back to Draft first.",
                ErrorCodes.QuestionLocked);
        }
    }
}
