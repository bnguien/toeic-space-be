using System.Globalization;
using ToeicSpace.Assessment.Application.Models.Attempts;

namespace ToeicSpace.Assessment.Application.Services.Attempts;

/// <summary>
/// Learner-facing attempt names, e.g. "Part 5 · 20 câu ngẫu nhiên · 18/11/2026".
/// </summary>
public static class AttemptTitles
{
    public const int MaximumLength = 200;

    private const string Separator = " · ";
    private const string RetakeWrongSuffix = " · Luyện lại câu sai";
    private const string RetakeAllSuffix = " · Làm lại";

    // Dates are shown in Vietnam time (UTC+7, no daylight saving).
    private static readonly TimeSpan LearnerOffset = TimeSpan.FromHours(7);

    public static string ForTest(string testTitle, AttemptMode mode, IReadOnlyCollection<ToeicPart> parts)
    {
        if (mode == AttemptMode.FullTest)
        {
            return Limit(testTitle);
        }

        var scope = parts.Count == AttemptParts.All.Count
            ? "Toàn bộ đề"
            : "Part " + string.Join(", ", parts.Order().Select(part => (int)part));

        return Limit(testTitle + Separator + scope);
    }

    public static string ForPracticeSet(
        SetSnapshot set,
        PracticeSelection selection,
        int questionCount,
        DateTime now)
    {
        var part = $"Part {(int)set.Part}";

        return Limit(selection switch
        {
            PracticeSelection.Continue => part + Separator + "Học tiếp",
            PracticeSelection.Random => part + Separator + $"{questionCount} câu ngẫu nhiên" + Separator + Date(now),
            PracticeSelection.Mistakes => part + Separator + "Ôn câu sai",
            _ => set.Title
        });
    }

    public static string ForRetake(string originalTitle, RetakeScope scope)
    {
        var baseTitle = originalTitle;

        foreach (var suffix in new[] { RetakeWrongSuffix, RetakeAllSuffix })
        {
            if (baseTitle.EndsWith(suffix, StringComparison.Ordinal))
            {
                baseTitle = baseTitle[..^suffix.Length];
            }
        }

        var suffixToAdd = scope == RetakeScope.Wrong ? RetakeWrongSuffix : RetakeAllSuffix;

        return Limit(Truncate(baseTitle, MaximumLength - suffixToAdd.Length) + suffixToAdd);
    }

    private static string Date(DateTime utcNow)
        => (AttemptAccess.Utc(utcNow) + LearnerOffset).ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);

    private static string Limit(string title) => Truncate(title.Trim(), MaximumLength);

    private static string Truncate(string value, int length) => value.Length <= length ? value : value[..length];
}
