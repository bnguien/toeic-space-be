using Microsoft.Extensions.Logging;
using ToeicSpace.Content.Application.DTOs.Vocabulary;
using ToeicSpace.Content.Application.Interfaces.Repositories;
using ToeicSpace.Content.Application.Mappings;
using ToeicSpace.Content.Domain.Entities;
using ToeicSpace.Content.Domain.Enums;
using ToeicSpace.Content.Domain.Exceptions;
using ToeicSpace.Content.Domain.Rules;

namespace ToeicSpace.Content.Application.Services;

/// <summary>
/// Records one review with <see cref="SpacedRepetitionScheduler"/> and the mistake flags; shared by <c>me/reviews</c>
/// and session answers.
/// Changes are staged only; the caller saves them (with its own changes) in one unit of work.
/// </summary>
public sealed class ReviewRecorder(ILearnerProgressRepository progress, IVocabularyCatalogRepository catalog,
    ILogger<ReviewRecorder> logger)
{
    /// <summary>The stored result when <paramref name="clientEventId"/> was already used, else null.</summary>
    public async Task<ReviewResultDto?> ReplayAsync(Guid learnerId, Guid wordId, Guid clientEventId, DateOnly today,
        CancellationToken ct)
    {
        var replay = await progress.GetReviewLogAsync(learnerId, clientEventId, ct);
        if (replay is null) return null;
        if (replay.WordId != wordId) throw VocabularyErrors.ClientEventConflict();
        var stored = (await progress.GetForWordsAsync(learnerId, [wordId], ct)).FirstOrDefault();
        return new ReviewResultDto(wordId, stored.ToProgressDto(today), stored.ToPreviewDto(today), true);
    }

    public async Task<ReviewResultDto> StageAsync(Guid learnerId, Guid wordId, StudyMode mode, ReviewGrade grade,
        Guid clientEventId, DateTime now, DateOnly today, CancellationToken ct)
    {
        // Official words and the learner's own custom words; another learner's private word is "not found".
        if (!await catalog.IsWordAccessibleAsync(wordId, learnerId, ct)) throw VocabularyErrors.WordNotFound();

        var row = await progress.GetForUpdateAsync(learnerId, wordId, ct);
        var before = row?.ToState();
        var result = SpacedRepetitionScheduler.Apply(before, grade, today);
        if (result.Changed)
        {
            if (row is null)
            {
                row = new LearnerWordProgress { LearnerId = learnerId, WordId = wordId, CreatedAt = now };
                progress.Add(row);
            }
            row.Apply(result.State, now);
        }
        else if (row is not null && grade != ReviewGrade.Seen)
        {
            // A second Good/Easy on the same day keeps the schedule but still counts as a review.
            row.LastReviewedAt = now;
            row.UpdatedAt = now;
            row.Version++;
        }
        // "Từ sai": Forget keeps the word for redo until a later Good/Easy (Hard and Seen leave it as it is).
        row?.TrackMistake(grade, now);

        progress.AddReviewLog(new WordReviewLog
        {
            LearnerId = learnerId, WordId = wordId, Mode = mode, Grade = grade, ClientEventId = clientEventId,
            ReviewedAt = now, LocalDay = today, IntervalBefore = before?.IntervalDays ?? 0,
            IntervalAfter = result.State.IntervalDays,
            EaseFactorBefore = before?.EaseFactor ?? SpacedRepetitionScheduler.DefaultEaseFactor,
            EaseFactorAfter = result.State.EaseFactor, ScheduleChanged = result.Changed
        });
        logger.LogInformation(
            "Learner {LearnerId} reviewed word {WordId} via {Mode} with {Grade}; interval {Before} -> {After} days",
            learnerId, wordId, mode, grade, before?.IntervalDays ?? 0, result.State.IntervalDays);
        return new ReviewResultDto(wordId, row.ToProgressDto(today), row.ToPreviewDto(today), false);
    }
}
