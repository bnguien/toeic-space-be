using ToeicSpace.Assessment.Application.Interfaces.Persistence;


namespace ToeicSpace.Assessment.Application.Services;

/// <summary>
/// Prevents changes that would break a test learners can currently take.
/// </summary>
public static class PublishedTestGuard
{
    public static async Task EnsureTestIsNotPublishedAsync(
        IAssessmentDbContext context,
        Guid? testId,
        string action,
        CancellationToken cancellationToken)
    {
        if (testId is not { } id)
        {
            return;
        }

        var isPublished = await context.ToeicTests
            .AnyAsync(test => test.Id == id && test.Status == ContentStatus.Active, cancellationToken);

        if (isPublished)
        {
            throw AppException.Conflict(
                $"Cannot {action} because the test is published. Move the test back to Draft first.",
                ErrorCodes.QuestionLocked);
        }
    }
}
