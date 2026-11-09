using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using ToeicSpace.Content.Application.Common.Json;
using ToeicSpace.Content.Application.DTOs.VocabularyLibrary;
using ToeicSpace.Content.Application.Features.VocabularyCatalog.Queries.GetStages;
using ToeicSpace.Content.Application.Features.VocabularyCatalog.Queries.GetStageSets;
using ToeicSpace.Content.Application.Features.VocabularyLibrary.Commands.CreateCustomWord;
using ToeicSpace.Content.Application.Features.VocabularyLibrary.Commands.CreateFolder;
using ToeicSpace.Content.Application.Features.VocabularyLibrary.Commands.DeleteFolder;
using ToeicSpace.Content.Application.Features.VocabularyLibrary.Commands.RemoveSavedWord;
using ToeicSpace.Content.Application.Features.VocabularyLibrary.Commands.SaveWord;
using ToeicSpace.Content.Application.Features.VocabularyLibrary.Commands.UpdateFolder;
using ToeicSpace.Content.Application.Features.VocabularyLibrary.Commands.UpdateSavedWord;
using ToeicSpace.Content.Application.Features.VocabularyLibrary.Queries.GetLibrary;
using ToeicSpace.Content.Application.Features.VocabularyLibrary.Queries.GetSavedWords;
using ToeicSpace.Content.Application.Features.VocabularyProgress.Commands.SubmitReview;
using ToeicSpace.Content.Application.Features.VocabularyProgress.Queries.GetLearnerSummary;
using ToeicSpace.Content.Application.Features.VocabularyProgress.Queries.GetStudyQueue;
using ToeicSpace.Content.Application.Features.VocabularyQuizzes.Commands.CreateQuiz;
using ToeicSpace.Content.Domain.Enums;
using ToeicSpace.Content.Domain.Exceptions;
using Xunit;

namespace ToeicSpace.Content.Tests.Vocabulary;

public sealed class VocabularyLibraryTests : IAsyncLifetime
{
    private readonly VocabularyTestContext _ctx = new();
    private Dictionary<string, Guid> _words = [];
    private Guid Learner => _ctx.Learner;

    public async Task InitializeAsync()
    {
        await _ctx.InitializeAsync();
        await _ctx.ImportAsync(
            StageJson.Build("stage-a", 1,
                new StageJson.S("lesson-1", 1, new("alpha"), new("beta"), new("gamma")),
                new StageJson.S("lesson-2", 2, new("delta"), new("echo"))),
            StageJson.Build("stage-b", 2, new StageJson.S("lesson-1", 1, new StageJson.W("zulu"))));
        _words = await _ctx.Db().VocabularyWords.ToDictionaryAsync(x => x.Headword, x => x.Id);
    }

    public Task DisposeAsync() => _ctx.DisposeAsync();

    private Task<FolderDto> Folder(string name, Guid? learner = null) =>
        _ctx.CreateFolder().Handle(new CreateFolderCommand(name, learner ?? Learner), default);

    private Task<SavedWordUpsertResult> Save(string word, Optional<Guid?> folder = default, Optional<string?> note = default) =>
        _ctx.SaveWord().Handle(new SaveWordCommand(_words[word], folder, note, 0, Learner), default);

    private Task<SavedWordDto> Custom(string headword, string pos = "Verb", Guid? folder = null, Guid? learner = null,
        string meaning = "nghĩa riêng") =>
        _ctx.CreateCustom().Handle(new CreateCustomWordCommand(headword, "/x/", Enum.Parse<PartOfSpeech>(pos), meaning,
            null, "An example.", null, folder, "my note", 0, learner ?? Learner), default);

    private static async Task<string> Code(Func<Task> action) =>
        (await Assert.ThrowsAsync<ContentRuleException>(action)).Code;

    [Fact]
    public async Task Folders_are_created_renamed_reordered_and_names_are_unique_per_learner()
    {
        var work = await Folder("Work");
        var travel = await Folder("  Travel ");
        Assert.Equal(("Work", 1, "Travel", 2), (work.Name, work.Order, travel.Name, travel.Order));
        Assert.Equal("folder_name_taken", await Code(() => Folder(" WORK ")));
        Assert.Equal("folder_name_taken", await Code(() => _ctx.UpdateFolder().Handle(
            new UpdateFolderCommand("work", null, 0, travel.Id, Learner), default)));
        Assert.Equal("Work", (await Folder("Work", Guid.NewGuid())).Name); // another learner

        var renamed = await _ctx.UpdateFolder().Handle(new UpdateFolderCommand("Trips", 5, 0, travel.Id, Learner), default);
        Assert.Equal(("Trips", 5), (renamed.Name, renamed.Order));
        var same = await _ctx.UpdateFolder().Handle(new UpdateFolderCommand("TRIPS", null, 0, travel.Id, Learner), default);
        Assert.Equal("TRIPS", same.Name); // renaming to its own name in another case is allowed
        Assert.Equal("vocabulary_folder_not_found", await Code(() => _ctx.UpdateFolder().Handle(
            new UpdateFolderCommand("x", null, 0, travel.Id, Guid.NewGuid()), default)));

        await Save("alpha", Optional<Guid?>.Of(work.Id));
        await _ctx.DeleteFolder().Handle(new DeleteFolderCommand(work.Id, Learner), default);
        var library = await _ctx.Library().Handle(new GetLibraryQuery(Learner), default);
        Assert.Equal((1, 1), (library.Total, library.Unfiled.WordCount));
        Assert.Equal(["TRIPS"], library.Folders.Select(x => x.Name));
        Assert.Equal("vocabulary_folder_not_found", await Code(() =>
            _ctx.DeleteFolder().Handle(new DeleteFolderCommand(work.Id, Learner), default)));
    }

    [Fact]
    public async Task Saving_an_official_word_is_idempotent_and_only_changes_fields_that_are_sent()
    {
        var folder = await Folder("Work");
        var first = await Save("alpha", Optional<Guid?>.Of(folder.Id), Optional<string?>.Of(" remember "));
        Assert.True(first.Created);
        Assert.Equal((folder.Id, "remember", false, "alpha"),
            (first.SavedWord.FolderId, first.SavedWord.Note, first.SavedWord.IsCustom, first.SavedWord.Word.Headword));

        var again = await Save("alpha");
        Assert.False(again.Created);
        Assert.Equal((first.SavedWord.Id, folder.Id, "remember"), (again.SavedWord.Id, again.SavedWord.FolderId, again.SavedWord.Note));
        var unfiled = await Save("alpha", Optional<Guid?>.Of(null));
        Assert.Null(unfiled.SavedWord.FolderId);
        Assert.Equal(1, await _ctx.Db().SavedWords.CountAsync());

        Assert.Equal("vocabulary_folder_not_found", await Code(() => Save("beta", Optional<Guid?>.Of(Guid.NewGuid()))));
        Assert.Equal("vocabulary_word_not_found", await Code(() => _ctx.SaveWord().Handle(
            new SaveWordCommand(Guid.NewGuid(), default, default, 0, Learner), default)));
        var foreign = await Custom("secret", learner: Guid.NewGuid());
        Assert.Equal("vocabulary_word_not_found", await Code(() => _ctx.SaveWord().Handle(
            new SaveWordCommand(foreign.Word.Id, default, default, 0, Learner), default)));
    }

    [Fact]
    public async Task Custom_words_are_private_unique_per_owner_and_editable()
    {
        var folder = await Folder("Mine");
        var custom = await Custom("Kick off", folder: folder.Id);
        Assert.True(custom.IsCustom);
        Assert.Equal(("Kick off", PartOfSpeech.Verb, folder.Id, "my note"),
            (custom.Word.Headword, custom.Word.PartOfSpeech, custom.FolderId, custom.Note));
        Assert.Equal(WordStatus.New, custom.Progress.Status);

        Assert.Equal("custom_word_exists", await Code(() => Custom("kick  OFF")));
        await Custom("kick off", "Noun");                  // other part of speech
        await Custom("kick off", learner: Guid.NewGuid()); // other learner
        await Custom("alpha", "Noun");                     // same key as an official word
        Assert.Equal(1, await _ctx.Db().VocabularyWords.CountAsync(x => x.OwnerKey == Guid.Empty && x.NormalizedHeadword == "alpha"));

        var edited = await _ctx.UpdateWord().Handle(new UpdateSavedWordCommand(Optional<Guid?>.Of(null),
            Optional<string?>.Of(null), new CustomWordInput("kickoff", null, PartOfSpeech.Noun, "khai mạc", "start", null, null),
            0, custom.Id, Learner), default);
        Assert.Equal(("kickoff", PartOfSpeech.Noun, "khai mạc", null, null, ""),
            (edited.Word.Headword, edited.Word.PartOfSpeech, edited.Word.MeaningVi, edited.FolderId, edited.Note, edited.Word.Phonetic));
        Assert.Equal("custom_word_exists", await Code(() => _ctx.UpdateWord().Handle(new UpdateSavedWordCommand(default,
            default, new CustomWordInput("Kick off", null, PartOfSpeech.Noun, "x", null, null, null), 0, custom.Id, Learner), default)));

        var official = (await Save("beta")).SavedWord;
        Assert.Equal("word_not_custom", await Code(() => _ctx.UpdateWord().Handle(new UpdateSavedWordCommand(default,
            default, new CustomWordInput("beta", null, PartOfSpeech.Noun, "x", null, null, null), 0, official.Id, Learner), default)));
        Assert.Equal("saved_word_not_found", await Code(() => _ctx.UpdateWord().Handle(
            new UpdateSavedWordCommand(default, Optional<string?>.Of("x"), null, 0, official.Id, Guid.NewGuid()), default)));
    }

    [Fact]
    public async Task Removing_a_custom_word_deletes_its_progress_but_unsaving_an_official_word_keeps_it()
    {
        var custom = await Custom("kickoff");
        var official = (await Save("alpha")).SavedWord;
        foreach (var wordId in new[] { custom.Word.Id, official.Word.Id })
            await _ctx.Reviewer().Handle(new SubmitReviewCommand(wordId, StudyMode.Flashcard, ReviewGrade.Good,
                Guid.NewGuid(), 0, Learner), default);
        Assert.Equal(2, await _ctx.Db().LearnerWordProgress.CountAsync());

        await _ctx.RemoveWord().Handle(new RemoveSavedWordCommand(custom.Id, Learner), default);
        await _ctx.RemoveWord().Handle(new RemoveSavedWordCommand(official.Id, Learner), default);
        var db = _ctx.Db();
        Assert.False(await db.VocabularyWords.AnyAsync(x => x.Id == custom.Word.Id));
        Assert.False(await db.WordReviewLogs.AnyAsync(x => x.WordId == custom.Word.Id));
        Assert.Equal([official.Word.Id], await db.LearnerWordProgress.Select(x => x.WordId).ToListAsync());
        Assert.True(await db.VocabularyWords.AnyAsync(x => x.Id == official.Word.Id));
        Assert.Empty(await db.SavedWords.ToListAsync());
        Assert.Equal("saved_word_not_found", await Code(() =>
            _ctx.RemoveWord().Handle(new RemoveSavedWordCommand(custom.Id, Learner), default)));
    }

    [Fact]
    public async Task Library_counts_and_word_list_filter_search_and_page()
    {
        var folder = await Folder("Work");
        await Save("alpha", Optional<Guid?>.Of(folder.Id));
        _ctx.Clock.Advance(TimeSpan.FromMinutes(1));
        await Save("beta");
        _ctx.Clock.Advance(TimeSpan.FromMinutes(1));
        await Custom("kickoff", folder: folder.Id, meaning: "khai mạc");
        await _ctx.Reviewer().Handle(new SubmitReviewCommand(_words["alpha"], StudyMode.Quiz, ReviewGrade.Good,
            Guid.NewGuid(), 0, Learner), default);

        var library = await _ctx.Library().Handle(new GetLibraryQuery(Learner), default);
        Assert.Equal(3, library.Total);
        Assert.Equal(new LibraryCountsDto(1, 0, 0, 0), library.Unfiled);
        Assert.Equal(new FolderDto(folder.Id, "Work", 1, 2, 1, 0, 0), Assert.Single(library.Folders));

        GetSavedWordsQuery Query(Guid? folderId = null, bool? unfiled = null, string? search = null, int page = 1,
            int size = 20) => new(Learner, folderId, unfiled, search, page, size);
        Assert.Equal(["kickoff", "beta", "alpha"], (await _ctx.SavedWords().Handle(Query(), default)).Items.Select(x => x.Word.Headword));
        Assert.Equal(["kickoff", "alpha"], (await _ctx.SavedWords().Handle(Query(folder.Id), default)).Items.Select(x => x.Word.Headword));
        Assert.Equal(["beta"], (await _ctx.SavedWords().Handle(Query(unfiled: true), default)).Items.Select(x => x.Word.Headword));
        Assert.Equal(["kickoff"], (await _ctx.SavedWords().Handle(Query(search: "khai"), default)).Items.Select(x => x.Word.Headword));
        var page = await _ctx.SavedWords().Handle(Query(page: 2, size: 2), default);
        Assert.Equal((3, "alpha", WordStatus.Learning), (page.TotalCount, Assert.Single(page.Items).Word.Headword,
            page.Items[0].Progress.Status));
        Assert.Equal("vocabulary_folder_not_found", await Code(() => _ctx.SavedWords().Handle(Query(Guid.NewGuid()), default)));
    }

    [Fact]
    public async Task Study_queue_covers_folders_and_the_library_and_marks_saved_words()
    {
        var folder = await Folder("Work");
        var alpha = (await Save("alpha", Optional<Guid?>.Of(folder.Id))).SavedWord;
        _ctx.Clock.Advance(TimeSpan.FromMinutes(1));
        var custom = await Custom("kickoff");

        var byFolder = await _ctx.Queue().Handle(new GetStudyQueueQuery(Learner, null, null, FolderId: folder.Id), default);
        Assert.Equal((alpha.Word.Id, alpha.Id), (Assert.Single(byFolder).Word.Id, byFolder[0].SavedWordId));
        var library = await _ctx.Queue().Handle(new GetStudyQueueQuery(Learner, null, null, Library: true), default);
        Assert.Equal(["kickoff", "alpha"], library.Select(x => x.Word.Headword));
        Assert.Equal(custom.Id, library[0].SavedWordId);

        var stageA = (await _ctx.Stages().Handle(new GetStagesQuery(), default))[0].Id;
        var lesson = (await _ctx.StageSets().Handle(new GetStageSetsQuery(stageA), default))[0].Id;
        var lessonQueue = await _ctx.Queue().Handle(new GetStudyQueueQuery(Learner, lesson, null), default);
        Assert.Equal([alpha.Id, null, null], lessonQueue.Select(x => x.SavedWordId));

        Assert.Equal("vocabulary_folder_not_found", await Code(() => _ctx.Queue().Handle(
            new GetStudyQueueQuery(Learner, null, null, FolderId: Guid.NewGuid()), default)));
        // A custom word is reviewed like any other word and shows up in the due queue.
        await _ctx.Reviewer().Handle(new SubmitReviewCommand(custom.Word.Id, StudyMode.Reel, ReviewGrade.Seen,
            Guid.NewGuid(), 0, Learner), default);
        _ctx.Clock.Advance(TimeSpan.FromDays(1));
        var due = await _ctx.Queue().Handle(new GetStudyQueueQuery(Learner, null, true), default);
        Assert.Equal((custom.Word.Id, custom.Id), (Assert.Single(due).Word.Id, due[0].SavedWordId));
        Assert.Equal("vocabulary_word_not_found", await Code(() => _ctx.Reviewer().Handle(new SubmitReviewCommand(
            custom.Word.Id, StudyMode.Flashcard, ReviewGrade.Good, Guid.NewGuid(), 0, Guid.NewGuid()), default)));
    }

    [Fact]
    public async Task Summary_reports_lessons_completed_next_lesson_and_library_size()
    {
        var empty = await _ctx.Summary().Handle(new GetLearnerSummaryQuery(Learner), default);
        Assert.Equal((3, 0, 0), (empty.LessonCount, empty.LessonsCompleted, empty.LibraryWordCount));
        Assert.Equal(("Lesson lesson-1", 1), (empty.NextSet!.Title, empty.NextSet.Order));
        Assert.Equal([(2, 0), (1, 0)], empty.Stages.Select(x => (x.SetCount, x.SetsCompleted)));

        foreach (var word in new[] { "alpha", "beta", "gamma", "delta" })
            await _ctx.Reviewer().Handle(new SubmitReviewCommand(_words[word], StudyMode.Flashcard, ReviewGrade.Hard,
                Guid.NewGuid(), 0, Learner), default);
        await Save("zulu");
        var summary = await _ctx.Summary().Handle(new GetLearnerSummaryQuery(Learner), default);
        Assert.Equal((3, 1, 1), (summary.LessonCount, summary.LessonsCompleted, summary.LibraryWordCount));
        var stageA = summary.Stages[0];
        Assert.Equal((2, 1), (stageA.SetCount, stageA.SetsCompleted));
        Assert.Equal((stageA.StageId, "Lesson lesson-2", 2), (summary.NextSet!.StageId, summary.NextSet.Title, summary.NextSet.Order));

        foreach (var word in new[] { "echo", "zulu" })
            await _ctx.Reviewer().Handle(new SubmitReviewCommand(_words[word], StudyMode.Flashcard, ReviewGrade.Good,
                Guid.NewGuid(), 0, Learner), default);
        var done = await _ctx.Summary().Handle(new GetLearnerSummaryQuery(Learner), default);
        Assert.Equal((3, (Application.DTOs.Vocabulary.NextSetDto?)null), (done.LessonsCompleted, done.NextSet));
    }

    [Fact]
    public async Task Quiz_uses_the_learners_own_words_then_official_words_of_the_same_part_of_speech()
    {
        var bike = await Custom("bike", "Noun", meaning: "xe đạp");
        await Custom("car", "Noun", meaning: "xe hơi");
        await Custom("bicycle", "Noun", meaning: "Xe đạp");
        await Custom("run fast", "Verb", meaning: "chạy nhanh");
        var officialNouns = new[] { "alpha", "beta", "gamma", "delta", "echo", "zulu" };
        for (var i = 0; i < 20; i++)
        {
            var question = Assert.Single(await _ctx.Quiz().Handle(new CreateQuizCommand([bike.Word.Id], Learner), default));
            var headwords = question.Options.Select(x => x.Headword).ToArray();
            Assert.Equal(4, headwords.Distinct().Count());
            Assert.Contains("bike", headwords);
            Assert.Contains("car", headwords);                // own noun first
            Assert.DoesNotContain("bicycle", headwords);      // same meaning as the prompt
            Assert.DoesNotContain("run fast", headwords);     // other part of speech
            Assert.Equal(2, headwords.Count(officialNouns.Contains));
        }
        // Private words are invisible to guests and other learners.
        Assert.Equal("vocabulary_word_not_found", await Code(() =>
            _ctx.Quiz().Handle(new CreateQuizCommand([bike.Word.Id]), default)));
        Assert.Equal("vocabulary_word_not_found", await Code(() =>
            _ctx.Quiz().Handle(new CreateQuizCommand([bike.Word.Id], Guid.NewGuid()), default)));
    }

    [Fact]
    public void Optional_fields_distinguish_absent_from_null_in_json()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        var absent = JsonSerializer.Deserialize<UpdateSavedWordCommand>("""{ "note": "x" }""", options)!;
        Assert.False(absent.FolderId.IsSet);
        Assert.Equal((true, "x"), (absent.Note.IsSet, absent.Note.Value));
        var cleared = JsonSerializer.Deserialize<UpdateSavedWordCommand>("""{ "folderId": null }""", options)!;
        Assert.Equal((true, (Guid?)null), (cleared.FolderId.IsSet, cleared.FolderId.Value));
        var moved = JsonSerializer.Deserialize<SaveWordCommand>(
            """{ "wordId": "6f9619ff-8b86-d011-b42d-00cf4fc964ff", "folderId": "7f9619ff-8b86-d011-b42d-00cf4fc964ff" }""", options)!;
        Assert.Equal(Guid.Parse("7f9619ff-8b86-d011-b42d-00cf4fc964ff"), moved.FolderId.Value);
    }
}
