using Microsoft.EntityFrameworkCore;
using ToeicSpace.Content.Application.DTOs.Vocabulary;
using ToeicSpace.Content.Application.Features.VocabularyCatalog.Queries.GetStages;
using ToeicSpace.Content.Application.Features.VocabularyCatalog.Queries.GetStageSets;
using ToeicSpace.Content.Application.Features.VocabularyProgress.Commands.SubmitReview;
using ToeicSpace.Content.Application.Features.VocabularyProgress.Queries.GetLearnerSummary;
using ToeicSpace.Content.Application.Features.VocabularyProgress.Queries.GetStageSetProgress;
using ToeicSpace.Content.Application.Features.VocabularyProgress.Queries.GetStudyQueue;
using ToeicSpace.Content.Domain.Entities;
using ToeicSpace.Content.Domain.Enums;
using ToeicSpace.Content.Domain.Exceptions;
using Xunit;

namespace ToeicSpace.Content.Tests.Vocabulary;

public sealed class VocabularyProgressTests : IAsyncLifetime
{
    private const int Vietnam = 420;
    private readonly VocabularyTestContext _ctx = new();
    private Dictionary<string, Guid> _words = [];
    private Guid _stageA, _lesson1, _lesson2;

    public async Task InitializeAsync()
    {
        await _ctx.InitializeAsync();
        await _ctx.ImportAsync(
            StageJson.Build("stage-a", 1,
                new StageJson.S("lesson-1", 1, new("alpha"), new("beta"), new("gamma")),
                new StageJson.S("lesson-2", 2, new("delta"), new("echo"))),
            StageJson.Build("stage-b", 2, new StageJson.S("lesson-1", 1, new StageJson.W("zulu"))));
        _words = await _ctx.Db().VocabularyWords.ToDictionaryAsync(x => x.Headword, x => x.Id);
        _stageA = (await _ctx.Stages().Handle(new GetStagesQuery(), default))[0].Id;
        var sets = await _ctx.StageSets().Handle(new GetStageSetsQuery(_stageA), default);
        (_lesson1, _lesson2) = (sets[0].Id, sets[1].Id);
    }

    public Task DisposeAsync() => _ctx.DisposeAsync();

    private Task<ReviewResultDto> Review(string word, ReviewGrade grade, int tz = Vietnam, Guid? eventId = null,
        StudyMode mode = StudyMode.Flashcard) =>
        _ctx.Reviewer().Handle(new SubmitReviewCommand(_words[word], mode, grade, eventId ?? Guid.NewGuid(), tz,
            _ctx.Learner), default);

    private Task<LearnerSummaryDto> Summary(int tz = Vietnam) =>
        _ctx.Summary().Handle(new GetLearnerSummaryQuery(_ctx.Learner, tz), default);

    [Fact]
    public async Task Review_creates_then_updates_progress_and_logs_every_review()
    {
        var first = await Review("alpha", ReviewGrade.Good);
        Assert.False(first.Duplicate);
        Assert.Equal(new ProgressDto(WordStatus.Learning, 1, 1, new DateOnly(2026, 11, 2), false,
            first.Progress.LastReviewedAt), first.Progress);
        Assert.Equal(DateTimeKind.Utc, first.Progress.LastReviewedAt!.Value.Kind);
        Assert.Equal(new SchedulePreviewDto(1, 1, 0, 0), first.Preview);

        // Same local day: Good keeps the schedule but is still logged.
        var kept = await Review("alpha", ReviewGrade.Good);
        Assert.Equal((1, 1), (kept.Progress.IntervalDays, kept.Progress.Repetitions));

        _ctx.Clock.Advance(TimeSpan.FromDays(1));
        var next = await Review("alpha", ReviewGrade.Good, mode: StudyMode.Quiz);
        Assert.Equal((6, 2), (next.Progress.IntervalDays, next.Progress.Repetitions));
        var forgot = await Review("alpha", ReviewGrade.Forget);
        Assert.Equal((1, 0), (forgot.Progress.IntervalDays, forgot.Progress.Repetitions));

        var db = _ctx.Db();
        var row = await db.LearnerWordProgress.SingleAsync();
        Assert.Equal((1, 1.96), (row.Lapses, row.EaseFactor));
        var logs = await db.WordReviewLogs.OrderBy(x => x.ReviewedAt).ThenBy(x => x.IntervalAfter).ToListAsync();
        Assert.Equal(4, logs.Count);
        Assert.Equal([true, false, true, true], logs.OrderBy(x => x.ReviewedAt).ThenByDescending(x => x.ScheduleChanged)
            .Select(x => x.ScheduleChanged));
        Assert.Contains(logs, x => x is { Mode: StudyMode.Quiz, IntervalBefore: 1, IntervalAfter: 6 });
    }

    [Fact]
    public async Task Seen_adds_a_new_word_and_never_changes_a_known_one()
    {
        var seen = await Review("beta", ReviewGrade.Seen, mode: StudyMode.Reel);
        Assert.Equal((WordStatus.Learning, 1, 0), (seen.Progress.Status, seen.Progress.IntervalDays, seen.Progress.Repetitions));

        _ctx.Clock.Advance(TimeSpan.FromDays(1));
        await Review("beta", ReviewGrade.Good);
        _ctx.Clock.Advance(TimeSpan.FromDays(1));
        var good = await Review("beta", ReviewGrade.Good);
        var again = await Review("beta", ReviewGrade.Seen, mode: StudyMode.Reel);
        Assert.Equal(good.Progress, again.Progress);
    }

    [Fact]
    public async Task Client_event_id_makes_reviews_idempotent()
    {
        var eventId = Guid.NewGuid();
        var first = await Review("alpha", ReviewGrade.Easy, eventId: eventId);
        var replay = await Review("alpha", ReviewGrade.Forget, eventId: eventId);
        Assert.True(replay.Duplicate);
        Assert.Equal(first.Progress, replay.Progress);
        Assert.Equal(1, await _ctx.Db().WordReviewLogs.CountAsync());

        Assert.Equal("client_event_conflict", (await Assert.ThrowsAsync<ContentRuleException>(() =>
            Review("beta", ReviewGrade.Good, eventId: eventId))).Code);
        Assert.Equal("vocabulary_word_not_found", (await Assert.ThrowsAsync<ContentRuleException>(() =>
            _ctx.Reviewer().Handle(new SubmitReviewCommand(Guid.NewGuid(), StudyMode.Flashcard, ReviewGrade.Good,
                Guid.NewGuid(), 0, _ctx.Learner), default))).Code);

        // The unique index is the final guard against two concurrent submissions of one event.
        var db = _ctx.Db();
        db.WordReviewLogs.Add(new WordReviewLog { LearnerId = _ctx.Learner, WordId = _words["alpha"], ClientEventId = eventId });
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }

    [Fact]
    public async Task Summary_counts_learning_mastered_due_and_uses_the_local_day()
    {
        // 18:00 UTC on 1 Nov is already 2 Nov in Vietnam (UTC+7).
        _ctx.Clock.Now = new DateTimeOffset(2026, 11, 1, 18, 0, 0, TimeSpan.Zero);
        await Review("alpha", ReviewGrade.Good);
        await Review("zulu", ReviewGrade.Good);
        var db = _ctx.Db();
        db.LearnerWordProgress.Add(new LearnerWordProgress
        {
            LearnerId = _ctx.Learner, WordId = _words["delta"], IntervalDays = 30, Repetitions = 4,
            DueOn = new DateOnly(2026, 11, 2)
        });
        db.LearnerWordProgress.Add(new LearnerWordProgress
        {
            LearnerId = Guid.NewGuid(), WordId = _words["beta"], IntervalDays = 1, DueOn = new DateOnly(2026, 1, 1)
        });
        await db.SaveChangesAsync();

        var vietnam = await Summary();
        Assert.Equal((3, 2, 1, 1, 1, 2), (vietnam.Learned, vietnam.Learning, vietnam.Mastered, vietnam.Due,
            vietnam.StreakDays, vietnam.ReviewedToday));
        Assert.Equal([(5, 1, 1), (1, 1, 0)], vietnam.Stages.Select(x => (x.WordCount, x.Learning, x.Mastered)));
        Assert.Equal(_stageA, vietnam.Stages[0].StageId);

        // In UTC it is still 1 Nov: delta is not due yet and today's reviews belong to "tomorrow".
        var utc = await Summary(0);
        Assert.Equal((0, 0, 0), (utc.Due, utc.ReviewedToday, utc.StreakDays));

        // Next local day both new words are due.
        _ctx.Clock.Advance(TimeSpan.FromDays(1));
        Assert.Equal(3, (await Summary()).Due);
    }

    [Fact]
    public async Task Streak_counts_consecutive_local_days_ending_today_or_yesterday()
    {
        foreach (var word in new[] { "alpha", "beta", "gamma" })
        {
            await Review(word, ReviewGrade.Good);
            _ctx.Clock.Advance(TimeSpan.FromDays(1));
        }
        Assert.Equal((3, 0), ((await Summary()).StreakDays, (await Summary()).ReviewedToday));
        await Review("delta", ReviewGrade.Hard);
        Assert.Equal((4, 1), ((await Summary()).StreakDays, (await Summary()).ReviewedToday));
        _ctx.Clock.Advance(TimeSpan.FromDays(2));
        Assert.Equal(0, (await Summary()).StreakDays);
        Assert.Equal(0, (await _ctx.Summary().Handle(new GetLearnerSummaryQuery(Guid.NewGuid()), default)).Learned);
    }

    [Fact]
    public async Task Set_progress_lists_only_sets_with_progress()
    {
        await Review("delta", ReviewGrade.Good);
        Assert.Equal([new SetProgressDto(_lesson2, 2, 1, 0, 0)],
            await _ctx.SetProgress().Handle(new GetStageSetProgressQuery(_ctx.Learner, _stageA, Vietnam), default));

        _ctx.Clock.Advance(TimeSpan.FromDays(1));
        await Review("alpha", ReviewGrade.Good);
        var progress = await _ctx.SetProgress().Handle(new GetStageSetProgressQuery(_ctx.Learner, _stageA, Vietnam), default);
        Assert.Equal([new SetProgressDto(_lesson1, 3, 1, 0, 0), new SetProgressDto(_lesson2, 2, 1, 0, 1)], progress);

        Assert.Equal("vocabulary_stage_not_found", (await Assert.ThrowsAsync<ContentRuleException>(() =>
            _ctx.SetProgress().Handle(new GetStageSetProgressQuery(_ctx.Learner, Guid.NewGuid()), default))).Code);
    }

    [Fact]
    public async Task Study_queue_by_set_returns_every_word_in_lesson_order()
    {
        await Review("beta", ReviewGrade.Good);
        var queue = await _ctx.Queue().Handle(new GetStudyQueueQuery(_ctx.Learner, _lesson1, null, 1, Vietnam), default);
        Assert.Equal(["alpha", "beta", "gamma"], queue.Select(x => x.Word.Headword));
        Assert.Equal([WordStatus.New, WordStatus.Learning, WordStatus.New], queue.Select(x => x.Progress.Status));
        Assert.Equal(new ProgressDto(WordStatus.New, 0, 0, null, false, null), queue[0].Progress);
        Assert.Equal(new SchedulePreviewDto(1, 1, 1, 1), queue[0].Preview);
        Assert.Equal(new SchedulePreviewDto(1, 1, 0, 0), queue[1].Preview);

        Assert.Equal("vocabulary_set_not_found", (await Assert.ThrowsAsync<ContentRuleException>(() =>
            _ctx.Queue().Handle(new GetStudyQueueQuery(_ctx.Learner, Guid.NewGuid(), null), default))).Code);
    }

    [Fact]
    public async Task Due_queue_orders_by_due_date_then_oldest_review_and_applies_limit()
    {
        _ctx.Clock.Now = new DateTimeOffset(2026, 11, 1, 12, 0, 0, TimeSpan.Zero);
        var today = new DateOnly(2026, 11, 1);
        var db = _ctx.Db();
        void Add(string word, DateOnly dueOn, DateTime? reviewed) => db.LearnerWordProgress.Add(new LearnerWordProgress
        {
            LearnerId = _ctx.Learner, WordId = _words[word], IntervalDays = 3, Repetitions = 2, DueOn = dueOn,
            LastReviewedAt = reviewed
        });
        Add("alpha", today, new DateTime(2026, 10, 29, 9, 0, 0));
        Add("beta", today.AddDays(-2), new DateTime(2026, 10, 27, 9, 0, 0));
        Add("gamma", today, new DateTime(2026, 10, 28, 9, 0, 0));
        Add("delta", today.AddDays(1), new DateTime(2026, 10, 29, 9, 0, 0));
        await db.SaveChangesAsync();

        var due = await _ctx.Queue().Handle(new GetStudyQueueQuery(_ctx.Learner, null, true, 20, 0), default);
        Assert.Equal(["beta", "gamma", "alpha"], due.Select(x => x.Word.Headword));
        Assert.All(due, x => Assert.True(x.Progress.IsDue));
        Assert.Equal(new SchedulePreviewDto(1, 1, 8, 8), due[0].Preview); // round(3 × 2.5) = 7.5 → 8

        var limited = await _ctx.Queue().Handle(new GetStudyQueueQuery(_ctx.Learner, null, true, 2, 0), default);
        Assert.Equal(["beta", "gamma"], limited.Select(x => x.Word.Headword));
        // At UTC+14 it is already 2 Nov locally, so delta is due too.
        Assert.Equal(4, (await _ctx.Queue().Handle(new GetStudyQueueQuery(_ctx.Learner, null, true, 20, 840), default)).Count);
    }
}
