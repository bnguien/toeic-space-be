using ToeicSpace.Content.Application.DTOs.Vocabulary;
using ToeicSpace.Content.Application.Interfaces.Repositories;
using ToeicSpace.Content.Application.Mappings;
using ToeicSpace.Content.Domain.Entities;
using ToeicSpace.Content.Domain.Rules;

namespace ToeicSpace.Content.Application.Features.VocabularyProgress.Queries.GetLearnerSummary;

public sealed class GetLearnerSummaryHandler(ILearnerProgressRepository progress,
    IVocabularyCatalogRepository catalog, ILearnerLibraryRepository library, IStudySessionRepository sessions,
    IVocabularyPreferencesRepository preferences, TimeProvider clock)
    : IRequestHandler<GetLearnerSummaryQuery, LearnerSummaryDto>
{
    public async Task<LearnerSummaryDto> Handle(GetLearnerSummaryQuery request, CancellationToken ct)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        var today = SpacedRepetitionScheduler.LocalDay(now, request.TzOffset);
        var rows = await progress.GetAllAsync(request.LearnerId, ct);
        var intervals = rows.ToDictionary(x => x.WordId, x => x.IntervalDays);
        var mastered = rows.Count(x => x.IntervalDays >= SpacedRepetitionScheduler.MasteredIntervalDays);
        var learning = rows.Count - mastered;
        var due = rows.Count(x => SpacedRepetitionScheduler.IsDue(x.DueOn, today));
        var streak = StreakDays(await progress.GetReviewDaysAsync(request.LearnerId, ct), today);
        var reviewedToday = await progress.CountReviewsOnAsync(request.LearnerId, today, ct);
        var libraryWordCount = await library.CountSavedWordsAsync(request.LearnerId, ct);

        // A lesson is started once any word has progress and completed once every word has.
        var sets = await catalog.GetSetOverviewsAsync(ct);
        var membership = await catalog.GetMembershipAsync(null, null, ct);
        var wordsBySet = membership.ToLookup(x => x.SetId, x => x.WordId);
        var completed = sets.Where(set => wordsBySet[set.SetId].All(intervals.ContainsKey))
            .Select(set => set.SetId).ToHashSet();
        var next = sets.FirstOrDefault(set => !completed.Contains(set.SetId));

        var stages = await catalog.GetStagesAsync(ct);
        var wordsByStage = membership.ToLookup(x => x.StageId, x => x.WordId);
        var setsByStage = sets.ToLookup(x => x.StageId);
        var stageProgress = stages.Select(stage =>
        {
            var words = wordsByStage[stage.Id].Distinct().ToArray();
            var known = words.Where(intervals.ContainsKey).Select(id => intervals[id]).ToArray();
            var stageMastered = known.Count(x => x >= SpacedRepetitionScheduler.MasteredIntervalDays);
            var stageSets = setsByStage[stage.Id].ToArray();
            return new StageProgressDto(stage.Id, words.Length, known.Length - stageMastered, stageMastered,
                stageSets.Length, stageSets.Count(x => completed.Contains(x.SetId)));
        }).ToArray();

        return new LearnerSummaryDto(learning + mastered, learning, mastered, due, streak, reviewedToday,
            sets.Count, completed.Count, libraryWordCount,
            next is null ? null : new NextSetDto(next.StageId, next.SetId, next.Title, next.SetOrder),
            stageProgress,
            (await sessions.GetLatestActiveAsync(request.LearnerId, null, null, null, now - StudySession.StaleAfter, ct))
                ?.ToActiveDto(),
            (await preferences.GetAsync(request.LearnerId, ct))?.DailyGoal ?? LearnerVocabularyPreferences.DefaultDailyGoal,
            rows.Count(x => x.MistakePending));
    }

    /// <summary>Consecutive local days with a review, ending today or yesterday.</summary>
    private static int StreakDays(IReadOnlyList<DateOnly> reviewDays, DateOnly today)
    {
        var days = reviewDays.ToHashSet();
        var cursor = days.Contains(today) ? today : today.AddDays(-1);
        var streak = 0;
        while (days.Contains(cursor))
        {
            streak++;
            cursor = cursor.AddDays(-1);
        }
        return streak;
    }
}
