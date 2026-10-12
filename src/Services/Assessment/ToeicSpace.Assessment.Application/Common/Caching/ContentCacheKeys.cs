namespace ToeicSpace.Assessment.Application.Common.Caching;

public static class ContentCacheKeys
{
    // Bump the version when the cached payload shape changes, so old entries are never served.
    private const string Prefix = "assessment:content:v2";

    public static string FullTest(
        Guid testId,
        bool canManageContent,
        bool includeAnswers)
        => $"{Prefix}:test:{testId:N}:{Audience(canManageContent)}:{(includeAnswers ? "answers" : "no-answers")}";

    public static string PracticeSet(
        Guid practiceSetId,
        bool canManageContent)
        => $"{Prefix}:practice-set:{practiceSetId:N}:{Audience(canManageContent)}";

    public static IEnumerable<string> AllFullTestVariants(Guid testId)
    {
        yield return FullTest(testId, canManageContent: false, includeAnswers: false);
        yield return FullTest(testId, canManageContent: true, includeAnswers: false);
        yield return FullTest(testId, canManageContent: true, includeAnswers: true);
    }

    public static IEnumerable<string> AllPracticeSetVariants(Guid practiceSetId)
    {
        yield return PracticeSet(practiceSetId, canManageContent: false);
        yield return PracticeSet(practiceSetId, canManageContent: true);
    }

    private static string Audience(bool canManageContent)
        => canManageContent ? "manager" : "learner";
}
