using ToeicSpace.Assessment.Application.Interfaces.Persistence;


namespace ToeicSpace.Assessment.Application.Services;

/// <summary>
/// Prevents changes that would leave a published practice set short of questions. Learners never see
/// questions that are not Active, so archiving or deleting one silently shrinks the set.
/// </summary>
public static class PublishedPracticeSetGuard
{
    private const int MaxCodesInMessage = 5;

    // The caller owns question/passage locks and has saved inside an uncommitted transaction.
    // Reuse publishing validation without taking locks in reverse order or rejecting valid bank edits.
    public static async Task EnsurePublishedContentRemainsValidAsync(
        IAssessmentDbContext context, IReadOnlyCollection<Guid> questionIds, CancellationToken cancellationToken)
    {
        var sets = await context.ToeicPracticeSetItems.AsNoTracking()
            .Where(item => questionIds.Contains(item.QuestionId) && item.PracticeSet.Status == ContentStatus.Active)
            .Select(item => item.PracticeSet).Distinct().ToListAsync(cancellationToken);
        foreach (var set in sets)
        {
            var ids = await context.ToeicPracticeSetItems.AsNoTracking()
                .Where(item => item.PracticeSetId == set.Id && questionIds.Contains(item.QuestionId)).Select(item => item.QuestionId).ToListAsync(cancellationToken);
            await PracticeSetPublishingGuard.EnsureValidAsync(context, set, ids, cancellationToken, lockContent: false);
        }
    }

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
