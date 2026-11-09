using Microsoft.EntityFrameworkCore;
using ToeicSpace.Content.Application.Common.Json;
using ToeicSpace.Content.Application.Common.Validation;
using ToeicSpace.Content.Application.DTOs.VocabularyPreferences;
using ToeicSpace.Content.Application.Features.StudySessions.Commands.AnswerStudySession;
using ToeicSpace.Content.Application.Features.StudySessions.Commands.StartStudySession;
using ToeicSpace.Content.Application.Features.StudySessions.Queries.GetActiveStudySession;
using ToeicSpace.Content.Application.Features.VocabularyLibrary.Commands.UpdatePersonalWord;
using ToeicSpace.Content.Application.Features.VocabularyLibrary.Queries.GetLibrary;
using ToeicSpace.Content.Application.Features.VocabularyPreferences.Commands.UpdatePreferences;
using ToeicSpace.Content.Application.Features.VocabularyPreferences.Queries.GetPreferences;
using ToeicSpace.Content.Application.Features.VocabularyProgress.Commands.SubmitReview;
using ToeicSpace.Content.Application.Features.VocabularyProgress.Queries.GetLearnerSummary;
using ToeicSpace.Content.Application.Features.VocabularyProgress.Queries.GetMistakes;
using ToeicSpace.Content.Application.Features.VocabularyProgress.Queries.GetStudyQueue;
using ToeicSpace.Content.Application.Services;
using ToeicSpace.Content.Domain.Entities;
using ToeicSpace.Content.Domain.Enums;
using ToeicSpace.Content.Domain.Exceptions;
using ToeicSpace.Content.Domain.Rules;
using Xunit;

namespace ToeicSpace.Content.Tests.Vocabulary;

/// <summary>"Từ sai" (mistake flags, list and study source) and the persisted study settings.</summary>
public sealed class MistakesAndSettingsTests : IAsyncLifetime
{
    private readonly VocabularyTestContext _ctx = new();
    private Dictionary<string, Guid> _words = [];
    private Guid Learner => _ctx.Learner;

    public async Task InitializeAsync()
    {
        await _ctx.InitializeAsync();
        await _ctx.ImportAsync(StageJson.Build("stage-a", 1,
            new StageJson.S("lesson-1", 1, new("alpha"), new("beta"), new("gamma"), new("delta"))));
        _words = await _ctx.Db().VocabularyWords.ToDictionaryAsync(x => x.Headword, x => x.Id);
    }

    public Task DisposeAsync() => _ctx.DisposeAsync();

    private Task Review(string word, ReviewGrade grade, Guid? learner = null) =>
        _ctx.Reviewer().Handle(new SubmitReviewCommand(_words[word], StudyMode.Quiz, grade, Guid.NewGuid(), 0,
            learner ?? Learner), default);

    private async Task<string[]> MistakeWords() =>
        (await _ctx.Mistakes().Handle(new GetMistakesQuery(Learner), default)).Items.Select(x => x.Word.Headword)
        .ToArray();

    private static async Task<string> Code(Func<Task> action) =>
        (await Assert.ThrowsAsync<ContentRuleException>(action)).Code;

    [Fact]
    public void Mistake_flags_follow_the_grade()
    {
        var first = new DateTime(2026, 11, 7, 1, 0, 0, DateTimeKind.Utc);
        var row = new LearnerWordProgress();
        (bool, int, DateTime?) State() => (row.MistakePending, row.MistakeCount, row.LastMistakeAt);

        row.TrackMistake(ReviewGrade.Forget, first);
        Assert.Equal((true, 1, (DateTime?)first), State());
        row.TrackMistake(ReviewGrade.Hard, first.AddMinutes(1));
        Assert.Equal((true, 1, (DateTime?)first), State());
        row.TrackMistake(ReviewGrade.Seen, first.AddMinutes(2));
        Assert.Equal((true, 1, (DateTime?)first), State());
        row.TrackMistake(ReviewGrade.Good, first.AddMinutes(3));
        Assert.Equal((false, 1, (DateTime?)first), State());
        row.TrackMistake(ReviewGrade.Forget, first.AddMinutes(4));
        Assert.Equal((true, 2, (DateTime?)first.AddMinutes(4)), State());
        row.TrackMistake(ReviewGrade.Easy, first.AddMinutes(5));
        Assert.Equal((false, 2, (DateTime?)first.AddMinutes(4)), State());
    }

    [Fact]
    public async Task Wrong_answers_stay_in_the_mistakes_list_until_answered_right()
    {
        await Review("alpha", ReviewGrade.Forget);
        _ctx.Clock.Advance(TimeSpan.FromMinutes(1));
        await Review("beta", ReviewGrade.Forget);
        await Review("gamma", ReviewGrade.Hard);
        await Review("delta", ReviewGrade.Good);
        await Review("alpha", ReviewGrade.Forget, Guid.NewGuid()); // another learner

        Assert.Equal(["beta", "alpha"], await MistakeWords()); // most recent mistake first
        var page = await _ctx.Mistakes().Handle(new GetMistakesQuery(Learner, 2, 1), default);
        var alpha = Assert.Single(page.Items);
        Assert.Equal(("alpha", 2, 1, (Guid?)null, _ctx.Clock.Now.UtcDateTime.AddMinutes(-1)),
            (alpha.Word.Headword, page.TotalCount, alpha.MistakeCount, alpha.SavedWordId, alpha.LastMistakeAt));
        Assert.Equal(DateTimeKind.Utc, alpha.LastMistakeAt.Kind);
        Assert.Equal(2, (await _ctx.Library().Handle(new GetLibraryQuery(Learner), default)).Mistakes);
        Assert.Equal(2, (await _ctx.Summary().Handle(new GetLearnerSummaryQuery(Learner), default)).Mistakes);

        // Hard and Seen keep the mistake; Good clears it; a later Forget counts again.
        await Review("alpha", ReviewGrade.Hard);
        await Review("alpha", ReviewGrade.Seen);
        Assert.Equal(["beta", "alpha"], await MistakeWords());
        await Review("beta", ReviewGrade.Good);
        Assert.Equal(["alpha"], await MistakeWords());
        _ctx.Clock.Advance(TimeSpan.FromMinutes(1));
        await Review("beta", ReviewGrade.Forget);
        var items = (await _ctx.Mistakes().Handle(new GetMistakesQuery(Learner), default)).Items;
        Assert.Equal((("beta", 2), ("alpha", 1)), ((items[0].Word.Headword, items[0].MistakeCount),
            (items[1].Word.Headword, items[1].MistakeCount)));

        // The list shows the learner's personal edits and the library entry.
        var saved = await _ctx.PersonalEdit().Handle(new UpdatePersonalWordCommand(
            MeaningVi: Optional<string?>.Of("nghĩa riêng"), WordId: _words["alpha"], LearnerId: Learner), default);
        var edited = (await _ctx.Mistakes().Handle(new GetMistakesQuery(Learner), default)).Items[1];
        Assert.Equal(("nghĩa riêng", saved.SavedWord.Id), (edited.Word.MeaningVi, edited.SavedWordId));
        Assert.Equal(["meaningVi"], edited.Word.Personal!);
    }

    [Fact]
    public async Task Mistakes_are_a_study_source_for_the_queue_and_sessions()
    {
        Assert.Equal("study_session_empty", await Code(() => _ctx.StartSession().Handle(
            new StartStudySessionCommand(StudyMode.Quiz, LearnerId: Learner, Mistakes: true), default)));
        await Review("alpha", ReviewGrade.Forget);
        _ctx.Clock.Advance(TimeSpan.FromMinutes(1));
        await Review("beta", ReviewGrade.Forget);

        var queue = await _ctx.Queue().Handle(new GetStudyQueueQuery(Learner, null, null, Mistakes: true), default);
        Assert.Equal(["beta", "alpha"], queue.Select(x => x.Word.Headword));

        var started = await _ctx.StartSession().Handle(
            new StartStudySessionCommand(StudyMode.Quiz, LearnerId: Learner, Mistakes: true), default);
        var session = started.Session;
        Assert.True(started.Created);
        Assert.Equal((StudySourceKind.Mistakes, (Guid?)null, StudyQueueService.MistakesTitle, 2),
            (session.Source.Kind, session.Source.Id, session.Source.Title, session.Total));
        Assert.Equal("Từ sai", session.Source.Title);
        Assert.Equal(session.Id, (await _ctx.ActiveSession().Handle(
            new GetActiveStudySessionQuery(Learner, StudyMode.Quiz, Mistakes: true), default))!.Id);
        Assert.False((await _ctx.StartSession().Handle(
            new StartStudySessionCommand(StudyMode.Quiz, LearnerId: Learner, Mistakes: true), default)).Created);

        // Answers go through the same recorder: Good clears the mistake, Forget counts it again.
        await _ctx.Answer().Handle(new AnswerStudySessionCommand(_words["beta"], ReviewGrade.Good, Guid.NewGuid(), 0,
            session.Id, Learner), default);
        await _ctx.Answer().Handle(new AnswerStudySessionCommand(_words["alpha"], ReviewGrade.Forget, Guid.NewGuid(), 0,
            session.Id, Learner), default);
        var left = Assert.Single((await _ctx.Mistakes().Handle(new GetMistakesQuery(Learner), default)).Items);
        Assert.Equal(("alpha", 2), (left.Word.Headword, left.MistakeCount));
    }

    [Fact]
    public void Mistakes_requests_are_validated()
    {
        Assert.True(new GetMistakesValidator().Validate(new GetMistakesQuery(Learner, 1, 100)).IsValid);
        Assert.False(new GetMistakesValidator().Validate(new GetMistakesQuery(Learner, 1, 101)).IsValid);
        Assert.False(new GetMistakesValidator().Validate(new GetMistakesQuery(Learner, 0)).IsValid);
        Assert.False(new GetMistakesValidator().Validate(new GetMistakesQuery(Guid.Empty)).IsValid);

        var start = new StartStudySessionValidator();
        Assert.True(start.Validate(new StartStudySessionCommand(StudyMode.Reel, LearnerId: Learner, Mistakes: true)).IsValid);
        Assert.False(start.Validate(new StartStudySessionCommand(StudyMode.Reel, Guid.NewGuid(), LearnerId: Learner,
            Mistakes: true)).IsValid);
        Assert.True(new GetStudyQueueValidator().Validate(new GetStudyQueueQuery(Learner, null, null, Mistakes: true)).IsValid);
        Assert.False(new GetStudyQueueValidator().Validate(new GetStudyQueueQuery(Learner, null, true, Mistakes: true)).IsValid);
        Assert.False(new GetActiveStudySessionValidator().Validate(
            new GetActiveStudySessionQuery(Learner, Library: true, Mistakes: true)).IsValid);
    }

    [Fact]
    public async Task Study_settings_default_and_merge_field_by_field()
    {
        var defaults = new StudySettingsDto(5, 15, true, true, false, "word", true, true, 0.8, "wood", "bell", "video",
            "stage", "portrait");
        Assert.Equal(defaults, (await _ctx.Preferences().Handle(new GetPreferencesQuery(Learner), default)).Study);

        var first = await _ctx.UpdatePreferences().Handle(new UpdatePreferencesCommand(LearnerId: Learner,
            Study: new StudySettingsInput(ReelSeconds: 8, TickStyle: "drum")), default);
        Assert.Equal(defaults with { ReelSeconds = 8, TickStyle = "drum" }, first.Study);

        var second = await _ctx.UpdatePreferences().Handle(new UpdatePreferencesCommand(LearnerId: Learner,
            Study: new StudySettingsInput(QuizSeconds: 0, SfxVolume: 0.35, FlashcardFront: "meaning", Sfx: false)), default);
        var merged = defaults with
        {
            ReelSeconds = 8, TickStyle = "drum", QuizSeconds = 0, SfxVolume = 0.35, FlashcardFront = "meaning", Sfx = false
        };
        Assert.Equal(merged, second.Study);

        // Other preference updates keep the study settings; reading them back returns what was stored.
        var other = await _ctx.UpdatePreferences().Handle(new UpdatePreferencesCommand(DailyGoal: Optional<int>.Of(30),
            LearnerId: Learner), default);
        Assert.Equal((30, merged), (other.DailyGoal, other.Study));
        Assert.Equal(merged, (await _ctx.Preferences().Handle(new GetPreferencesQuery(Learner), default)).Study);
        Assert.Equal(defaults, (await _ctx.Preferences().Handle(new GetPreferencesQuery(Guid.NewGuid()), default)).Study);
        var stored = (await _ctx.Db().LearnerVocabularyPreferences.SingleAsync()).StudySettings!;
        Assert.Equal((8, "drum", 0.35), (stored.ReelSeconds, stored.TickStyle, stored.SfxVolume));
    }

    [Fact]
    public void Study_settings_reject_unknown_or_out_of_range_values()
    {
        var validator = new StudySettingsInputValidator();
        Assert.True(validator.Validate(new StudySettingsInput()).IsValid);
        Assert.True(validator.Validate(new StudySettingsInput(3, 30, true, false, true, "meaning", false, true, 0.1,
            "off", "soft", "ambient", "night", "landscape")).IsValid);
        foreach (var volume in new[] { 0.1, 0.55, 0.95, 1.0 })
            Assert.True(validator.Validate(new StudySettingsInput(SfxVolume: volume)).IsValid, $"{volume}");
        foreach (var volume in new[] { 0.0, 0.05, 0.33, 1.05 })
            Assert.False(validator.Validate(new StudySettingsInput(SfxVolume: volume)).IsValid, $"{volume}");
        Assert.False(validator.Validate(new StudySettingsInput(ReelSeconds: 4)).IsValid);
        Assert.False(validator.Validate(new StudySettingsInput(QuizSeconds: 5)).IsValid);
        Assert.False(validator.Validate(new StudySettingsInput(FlashcardFront: "Word")).IsValid);
        Assert.False(validator.Validate(new StudySettingsInput(TickStyle: "bell")).IsValid);
        Assert.False(validator.Validate(new StudySettingsInput(ChimeStyle: "wood")).IsValid);
        Assert.False(validator.Validate(new StudySettingsInput(Backdrop: "image")).IsValid);
        Assert.False(validator.Validate(new StudySettingsInput(CardStyle: "dark")).IsValid);
        Assert.False(validator.Validate(new StudySettingsInput(Layout: "Landscape")).IsValid);
        Assert.False(validator.Validate(new StudySettingsInput(Layout: "square")).IsValid);

        var preferences = new UpdatePreferencesValidator();
        Assert.False(preferences.Validate(new UpdatePreferencesCommand(LearnerId: Learner,
            Study: new StudySettingsInput(ReelSeconds: 7))).IsValid);
        Assert.True(preferences.Validate(new UpdatePreferencesCommand(LearnerId: Learner,
            Study: new StudySettingsInput(ReelSeconds: 12))).IsValid);
        Assert.Equal(StudySettings.Default.ReelSeconds, new StudySettingsInput().ApplyTo(StudySettings.Default).ReelSeconds);
    }
}
