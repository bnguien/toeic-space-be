using Microsoft.EntityFrameworkCore;
using ToeicSpace.Content.Application.Common.Json;
using ToeicSpace.Content.Application.DTOs.StudySessions;
using ToeicSpace.Content.Application.Features.StudySessions.Commands.AbandonStudySession;
using ToeicSpace.Content.Application.Features.StudySessions.Commands.AnswerStudySession;
using ToeicSpace.Content.Application.Features.StudySessions.Commands.CompleteStudySession;
using ToeicSpace.Content.Application.Features.StudySessions.Commands.StartStudySession;
using ToeicSpace.Content.Application.Features.StudySessions.Queries.GetActiveStudySession;
using ToeicSpace.Content.Application.Features.StudySessions.Queries.GetStudySession;
using ToeicSpace.Content.Application.Features.VocabularyCatalog.Queries.GetStages;
using ToeicSpace.Content.Application.Features.VocabularyCatalog.Queries.GetStageSets;
using ToeicSpace.Content.Application.Features.VocabularyPreferences.Commands.UpdatePreferences;
using ToeicSpace.Content.Application.Features.VocabularyPreferences.Queries.GetPreferences;
using ToeicSpace.Content.Application.Features.VocabularyProgress.Queries.GetLearnerSummary;
using ToeicSpace.Content.Domain.Entities;
using ToeicSpace.Content.Domain.Enums;
using ToeicSpace.Content.Domain.Exceptions;
using Xunit;

namespace ToeicSpace.Content.Tests.Vocabulary;

public sealed class StudySessionTests : IAsyncLifetime
{
    private readonly VocabularyTestContext _ctx = new();
    private Dictionary<string, Guid> _words = [];
    private Guid _lesson;
    private Guid Learner => _ctx.Learner;

    public async Task InitializeAsync()
    {
        await _ctx.InitializeAsync();
        await _ctx.ImportAsync(StageJson.Build("stage-a", 1,
            new StageJson.S("lesson-1", 1, new("alpha"), new("beta"), new("gamma")),
            new StageJson.S("lesson-2", 2, new("delta"), new("echo"))));
        _words = await _ctx.Db().VocabularyWords.ToDictionaryAsync(x => x.Headword, x => x.Id);
        var stage = (await _ctx.Stages().Handle(new GetStagesQuery(), default))[0].Id;
        _lesson = (await _ctx.StageSets().Handle(new GetStageSetsQuery(stage), default))[0].Id;
    }

    public Task DisposeAsync() => _ctx.DisposeAsync();

    private Task<StudySessionStartResult> Start(StudyMode mode = StudyMode.Flashcard, bool restart = false,
        Guid? setId = null, bool? due = null, IReadOnlyList<Guid>? words = null) =>
        _ctx.StartSession().Handle(new StartStudySessionCommand(mode, setId ?? (due is null && words is null ? _lesson : null),
            Due: due, WordIds: words, Restart: restart, LearnerId: Learner), default);

    private Task<StudySessionDto> Answer(Guid session, string word, ReviewGrade grade, Guid? eventId = null) =>
        _ctx.Answer().Handle(new AnswerStudySessionCommand(_words[word], grade, eventId ?? Guid.NewGuid(), 0, session,
            Learner), default);

    private static async Task<string> Code(Func<Task> action) =>
        (await Assert.ThrowsAsync<ContentRuleException>(action)).Code;

    [Fact]
    public async Task Starting_again_resumes_the_active_session_unless_restart_is_requested()
    {
        var first = await Start();
        Assert.True(first.Created);
        Assert.Equal((StudySourceKind.Set, _lesson, "Lesson lesson-1", 3, 0, StudySessionStatus.Active),
            (first.Session.Source.Kind, first.Session.Source.Id, first.Session.Source.Title, first.Session.Total,
                first.Session.Position, first.Session.Status));
        Assert.Equal(["alpha", "beta", "gamma"], first.Session.Items.Select(x => x.Word.Headword));
        await Answer(first.Session.Id, "alpha", ReviewGrade.Good);

        var resumed = await Start();
        Assert.False(resumed.Created);
        Assert.Equal((first.Session.Id, 1), (resumed.Session.Id, resumed.Session.Position));
        Assert.NotEqual(first.Session.Id, (await Start(StudyMode.Quiz)).Session.Id); // other mode, other session

        var restarted = await Start(restart: true);
        Assert.True(restarted.Created);
        Assert.NotEqual(first.Session.Id, restarted.Session.Id);
        var old = await _ctx.GetSession().Handle(new GetStudySessionQuery(Learner, first.Session.Id), default);
        Assert.Equal(StudySessionStatus.Abandoned, old.Status);
        Assert.Equal(restarted.Session.Id, (await _ctx.ActiveSession().Handle(
            new GetActiveStudySessionQuery(Learner, StudyMode.Flashcard, SetId: _lesson), default))!.Id);
    }

    [Fact]
    public async Task Answers_record_reviews_requeue_forget_or_hard_once_and_completion_summarises()
    {
        var session = (await Start()).Session;
        var afterForget = await Answer(session.Id, "alpha", ReviewGrade.Forget);
        Assert.Equal(["alpha", "beta", "gamma", "alpha"], afterForget.Items.Select(x => x.Word.Headword));
        Assert.Equal((1, 4, WordStatus.Learning), (afterForget.Position, afterForget.Total, afterForget.Review!.Progress.Status));
        Assert.False(afterForget.Review.Duplicate);

        await Answer(session.Id, "beta", ReviewGrade.Good);
        await Answer(session.Id, "gamma", ReviewGrade.Easy);
        var last = await Answer(session.Id, "alpha", ReviewGrade.Hard); // already re-queued once
        Assert.Equal((4, 4, 2, 4), (last.Position, last.Total, last.CorrectCount, last.ReviewCount));
        Assert.Null(last.Summary);
        Assert.Equal(4, await _ctx.Db().WordReviewLogs.CountAsync());
        Assert.Equal(3, await _ctx.Db().LearnerWordProgress.CountAsync());

        var done = await _ctx.CompleteSession().Handle(new CompleteStudySessionCommand(session.Id, Learner), default);
        Assert.Equal(StudySessionStatus.Completed, done.Status);
        Assert.NotNull(done.CompletedAt);
        Assert.Equal(new SessionSummaryDto(2, 1, [_words["alpha"]]), done.Summary! with { WordIds = done.Summary.WordIds.ToArray() },
            new SummaryComparer());
        Assert.Equal(ReviewGrade.Hard, done.Results.Single(x => x.WordId == _words["alpha"]).Grade);
        Assert.Equal(done.Summary, (await _ctx.CompleteSession().Handle(
            new CompleteStudySessionCommand(session.Id, Learner), default)).Summary, new SummaryComparer());
        Assert.Equal("study_session_not_active", await Code(() => Answer(session.Id, "beta", ReviewGrade.Good)));
        Assert.Null(await _ctx.ActiveSession().Handle(new GetActiveStudySessionQuery(Learner), default));
    }

    [Fact]
    public async Task Replayed_answers_are_idempotent_and_answers_must_match_the_current_card()
    {
        var session = (await Start()).Session;
        var eventId = Guid.NewGuid();
        var first = await Answer(session.Id, "alpha", ReviewGrade.Good, eventId);
        var replay = await Answer(session.Id, "alpha", ReviewGrade.Good, eventId);
        Assert.Equal((first.Position, first.ReviewCount, true), (replay.Position, replay.ReviewCount, replay.Review!.Duplicate));
        Assert.Equal(1, await _ctx.Db().WordReviewLogs.CountAsync());

        Assert.Equal("study_session_word_mismatch", await Code(() => Answer(session.Id, "gamma", ReviewGrade.Good)));
        Assert.Equal("client_event_conflict", await Code(() => Answer(session.Id, "beta", ReviewGrade.Good, eventId)));
        Assert.Equal("study_session_not_found", await Code(() => _ctx.Answer().Handle(new AnswerStudySessionCommand(
            _words["beta"], ReviewGrade.Good, Guid.NewGuid(), 0, session.Id, Guid.NewGuid()), default)));
    }

    [Fact]
    public async Task Sessions_idle_for_more_than_seven_days_are_abandoned_and_can_be_abandoned_explicitly()
    {
        var session = (await Start()).Session;
        var summary = await _ctx.Summary().Handle(new GetLearnerSummaryQuery(Learner), default);
        Assert.Equal((session.Id, 0, 3), (summary.ActiveSession!.Id, summary.ActiveSession.Position, summary.ActiveSession.Total));

        _ctx.Clock.Advance(TimeSpan.FromDays(8));
        Assert.Equal(StudySessionStatus.Abandoned,
            (await _ctx.GetSession().Handle(new GetStudySessionQuery(Learner, session.Id), default)).Status);
        Assert.Null(await _ctx.ActiveSession().Handle(new GetActiveStudySessionQuery(Learner), default));
        Assert.Null((await _ctx.Summary().Handle(new GetLearnerSummaryQuery(Learner), default)).ActiveSession);
        Assert.Equal("study_session_not_active", await Code(() => Answer(session.Id, "alpha", ReviewGrade.Good)));

        var fresh = await Start();
        Assert.True(fresh.Created);
        await _ctx.AbandonSession().Handle(new AbandonStudySessionCommand(fresh.Session.Id, Learner), default);
        await _ctx.AbandonSession().Handle(new AbandonStudySessionCommand(fresh.Session.Id, Learner), default);
        Assert.Equal(StudySessionStatus.Abandoned,
            (await _ctx.GetSession().Handle(new GetStudySessionQuery(Learner, fresh.Session.Id), default)).Status);
        Assert.Equal("study_session_not_active", await Code(() =>
            _ctx.CompleteSession().Handle(new CompleteStudySessionCommand(fresh.Session.Id, Learner), default)));
        // Both the expired session (abandoned when the new one started) and the explicit one; no slot is held.
        Assert.Equal(2, await _ctx.Db().StudySessions.CountAsync(x => x.ActiveSlot == null && x.Status == StudySessionStatus.Abandoned));
    }

    [Fact]
    public async Task Due_and_word_list_sessions()
    {
        Assert.Equal("study_session_empty", await Code(() => Start(due: true)));
        var words = new[] { _words["echo"], _words["delta"] };
        var list = await Start(StudyMode.Reel, words: words);
        Assert.Equal(StudySourceKind.Words, list.Session.Source.Kind);
        Assert.Equal(words, list.Session.Items.Select(x => x.Word.Id));
        var again = await Start(StudyMode.Reel, words: words);
        Assert.True(again.Created); // word lists always start fresh
        Assert.Equal("vocabulary_word_not_found", await Code(() => Start(words: [Guid.NewGuid()])));

        await Answer(again.Session.Id, "echo", ReviewGrade.Seen);
        _ctx.Clock.Advance(TimeSpan.FromDays(1));
        var due = await Start(StudyMode.Quiz, due: true);
        Assert.Equal(StudySourceKind.Due, due.Session.Source.Kind);
        Assert.Equal(["echo"], due.Session.Items.Select(x => x.Word.Headword));
        Assert.Equal(ReviewGrade.Seen, (await _ctx.Db().WordReviewLogs.SingleAsync()).Grade);
        Assert.Equal(StudyMode.Reel, (await _ctx.Db().WordReviewLogs.SingleAsync()).Mode);
    }

    [Fact]
    public async Task Preferences_default_update_partially_and_feed_the_daily_goal()
    {
        var defaults = await _ctx.Preferences().Handle(new GetPreferencesQuery(Learner), default);
        Assert.Equal((1.0, false, true, true, StudyMode.Flashcard, 20, VocabularyView.Roadmap, (DateTime?)null),
            (defaults.FontScale, defaults.SidebarCollapsed, defaults.Snowfall, defaults.AutoPlayAudio, defaults.DefaultMode,
                defaults.DailyGoal, defaults.LastView, defaults.UpdatedAt));

        var folder = Guid.NewGuid();
        var updated = await _ctx.UpdatePreferences().Handle(new UpdatePreferencesCommand(
            FontScale: Optional<double>.Of(1.125), DailyGoal: Optional<int>.Of(35), LastView: Optional<VocabularyView>.Of(VocabularyView.Library),
            LastFolderId: Optional<Guid?>.Of(folder), LearnerId: Learner), default);
        Assert.Equal((1.125, 35, VocabularyView.Library, folder, true), (updated.FontScale, updated.DailyGoal,
            updated.LastView, updated.LastFolderId, updated.Snowfall));
        Assert.NotNull(updated.UpdatedAt);

        var partial = await _ctx.UpdatePreferences().Handle(new UpdatePreferencesCommand(
            Snowfall: Optional<bool>.Of(false), LastFolderId: Optional<Guid?>.Of(null), LearnerId: Learner), default);
        Assert.Equal((1.125, 35, false, (Guid?)null), (partial.FontScale, partial.DailyGoal, partial.Snowfall, partial.LastFolderId));
        Assert.Equal(1, await _ctx.Db().LearnerVocabularyPreferences.CountAsync());
        Assert.Equal(35, (await _ctx.Summary().Handle(new GetLearnerSummaryQuery(Learner), default)).DailyGoal);
        Assert.Equal(20, (await _ctx.Summary().Handle(new GetLearnerSummaryQuery(Guid.NewGuid()), default)).DailyGoal);

        var validator = new UpdatePreferencesValidator();
        Assert.True(validator.Validate(new UpdatePreferencesCommand(LearnerId: Learner)).IsValid);
        Assert.False(validator.Validate(new UpdatePreferencesCommand(FontScale: Optional<double>.Of(0.9), LearnerId: Learner)).IsValid);
        Assert.False(validator.Validate(new UpdatePreferencesCommand(DailyGoal: Optional<int>.Of(4), LearnerId: Learner)).IsValid);
        Assert.False(validator.Validate(new UpdatePreferencesCommand(DailyGoal: Optional<int>.Of(201), LearnerId: Learner)).IsValid);
        Assert.False(validator.Validate(new UpdatePreferencesCommand(DefaultMode: Optional<StudyMode>.Of((StudyMode)7), LearnerId: Learner)).IsValid);
        Assert.False(validator.Validate(new UpdatePreferencesCommand(LastStageId: Optional<Guid?>.Of(Guid.Empty), LearnerId: Learner)).IsValid);
        Assert.True(validator.Validate(new UpdatePreferencesCommand(SidebarWidth: Optional<int?>.Of(220), LearnerId: Learner)).IsValid);
        Assert.True(validator.Validate(new UpdatePreferencesCommand(SidebarWidth: Optional<int?>.Of(420), LearnerId: Learner)).IsValid);
        Assert.True(validator.Validate(new UpdatePreferencesCommand(SidebarWidth: Optional<int?>.Of(null), LearnerId: Learner)).IsValid);
        Assert.False(validator.Validate(new UpdatePreferencesCommand(SidebarWidth: Optional<int?>.Of(219), LearnerId: Learner)).IsValid);
        Assert.False(validator.Validate(new UpdatePreferencesCommand(SidebarWidth: Optional<int?>.Of(421), LearnerId: Learner)).IsValid);

        Assert.Null(defaults.SidebarWidth);
        var wide = await _ctx.UpdatePreferences().Handle(new UpdatePreferencesCommand(
            SidebarWidth: Optional<int?>.Of(300), LearnerId: Learner), default);
        Assert.Equal((300, 35), (wide.SidebarWidth, wide.DailyGoal));
        var reset = await _ctx.UpdatePreferences().Handle(new UpdatePreferencesCommand(
            SidebarWidth: Optional<int?>.Of(null), LearnerId: Learner), default);
        Assert.Null(reset.SidebarWidth);
        Assert.Null((await _ctx.UpdatePreferences().Handle(new UpdatePreferencesCommand(
            Snowfall: Optional<bool>.Of(true), LearnerId: Learner), default)).SidebarWidth);
    }

    [Fact]
    public void Requeue_inserts_the_card_four_cards_later_or_at_the_end()
    {
        var ids = Enumerable.Range(0, 8).Select(_ => Guid.NewGuid()).ToList();
        var session = new StudySession { Queue = [.. ids] };
        session.Answer(ids[0], ReviewGrade.Forget, DateTime.UtcNow);
        Assert.Equal(ids[0], session.Queue[5]); // ids[1..4] come first
        session.Answer(ids[1], ReviewGrade.Seen, DateTime.UtcNow);
        Assert.Equal(9, session.Queue.Count);   // Seen is not re-queued
    }

    private sealed class SummaryComparer : IEqualityComparer<SessionSummaryDto?>
    {
        public bool Equals(SessionSummaryDto? x, SessionSummaryDto? y) => x is not null && y is not null
            && x.Remembered == y.Remembered && x.NeedReview == y.NeedReview && x.WordIds.SequenceEqual(y.WordIds);
        public int GetHashCode(SessionSummaryDto? obj) => 0;
    }
}
