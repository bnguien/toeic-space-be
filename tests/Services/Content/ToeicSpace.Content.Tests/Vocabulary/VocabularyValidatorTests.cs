using ToeicSpace.Content.Application.Common.Json;
using ToeicSpace.Content.Application.DTOs.VocabularyImport;
using ToeicSpace.Content.Application.DTOs.VocabularyLibrary;
using ToeicSpace.Content.Application.Features.StudySessions.Commands.AnswerStudySession;
using ToeicSpace.Content.Application.Features.StudySessions.Commands.StartStudySession;
using ToeicSpace.Content.Application.Features.StudySessions.Queries.GetActiveStudySession;
using ToeicSpace.Content.Application.Features.VocabularyLibrary.Commands.CreateCustomWord;
using ToeicSpace.Content.Application.Features.VocabularyLibrary.Commands.CreateFolder;
using ToeicSpace.Content.Application.Features.VocabularyLibrary.Commands.SaveWord;
using ToeicSpace.Content.Application.Features.VocabularyLibrary.Commands.UpdateFolder;
using ToeicSpace.Content.Application.Features.VocabularyLibrary.Commands.UpdateSavedWord;
using ToeicSpace.Content.Application.Features.VocabularyLibrary.Queries.GetSavedWords;
using ToeicSpace.Content.Application.Features.ReelBackgrounds.Queries.GetReelBackgrounds;
using ToeicSpace.Content.Application.Features.VocabularyCatalog.Queries.GetSetDetail;
using ToeicSpace.Content.Application.Features.VocabularyCatalog.Queries.GetStageSets;
using ToeicSpace.Content.Application.Features.VocabularyImport.Commands.ImportVocabularyStages;
using ToeicSpace.Content.Application.Features.VocabularyProgress.Commands.SubmitReview;
using ToeicSpace.Content.Application.Features.VocabularyProgress.Queries.GetLearnerSummary;
using ToeicSpace.Content.Application.Features.VocabularyProgress.Queries.GetStageSetProgress;
using ToeicSpace.Content.Application.Features.VocabularyProgress.Queries.GetStudyQueue;
using ToeicSpace.Content.Application.Features.VocabularyQuizzes.Commands.CreateQuiz;
using ToeicSpace.Content.Domain.Enums;
using Xunit;

namespace ToeicSpace.Content.Tests.Vocabulary;

public sealed class VocabularyValidatorTests
{
    private static readonly Guid Learner = Guid.NewGuid();

    [Theory]
    [InlineData(-720, true)]
    [InlineData(840, true)]
    [InlineData(0, true)]
    [InlineData(-721, false)]
    [InlineData(841, false)]
    public void TzOffset_must_be_between_minus_720_and_840(int tz, bool valid)
    {
        Assert.Equal(valid, new GetLearnerSummaryValidator().Validate(new GetLearnerSummaryQuery(Learner, tz)).IsValid);
        Assert.Equal(valid, new GetStageSetProgressValidator()
            .Validate(new GetStageSetProgressQuery(Learner, Guid.NewGuid(), tz)).IsValid);
        Assert.Equal(valid, new GetStudyQueueValidator()
            .Validate(new GetStudyQueueQuery(Learner, null, true, 20, tz)).IsValid);
        Assert.Equal(valid, new SubmitReviewValidator().Validate(new SubmitReviewCommand(Guid.NewGuid(),
            StudyMode.Reel, ReviewGrade.Seen, Guid.NewGuid(), tz, Learner)).IsValid);
    }

    [Fact]
    public void Study_queue_needs_exactly_one_source_and_a_limit_from_1_to_100()
    {
        var validator = new GetStudyQueueValidator();
        Assert.True(validator.Validate(new GetStudyQueueQuery(Learner, Guid.NewGuid(), null)).IsValid);
        Assert.True(validator.Validate(new GetStudyQueueQuery(Learner, Guid.NewGuid(), false)).IsValid);
        Assert.True(validator.Validate(new GetStudyQueueQuery(Learner, null, true, 100)).IsValid);
        Assert.False(validator.Validate(new GetStudyQueueQuery(Learner, Guid.NewGuid(), true)).IsValid);
        Assert.False(validator.Validate(new GetStudyQueueQuery(Learner, null, null)).IsValid);
        Assert.False(validator.Validate(new GetStudyQueueQuery(Learner, null, false)).IsValid);
        Assert.False(validator.Validate(new GetStudyQueueQuery(Learner, Guid.Empty, null)).IsValid);
        Assert.False(validator.Validate(new GetStudyQueueQuery(Learner, null, true, 0)).IsValid);
        Assert.False(validator.Validate(new GetStudyQueueQuery(Learner, null, true, 101)).IsValid);
        Assert.False(validator.Validate(new GetStudyQueueQuery(Guid.Empty, null, true)).IsValid);
    }

    [Fact]
    public void Quiz_accepts_1_to_50_non_empty_word_ids()
    {
        var validator = new CreateQuizValidator();
        Assert.True(validator.Validate(new CreateQuizCommand([Guid.NewGuid()])).IsValid);
        Assert.True(validator.Validate(new CreateQuizCommand(Enumerable.Range(0, 50).Select(_ => Guid.NewGuid()).ToArray())).IsValid);
        Assert.False(validator.Validate(new CreateQuizCommand([])).IsValid);
        Assert.False(validator.Validate(new CreateQuizCommand(null!)).IsValid);
        Assert.False(validator.Validate(new CreateQuizCommand(Enumerable.Range(0, 51).Select(_ => Guid.NewGuid()).ToArray())).IsValid);
        Assert.False(validator.Validate(new CreateQuizCommand([Guid.NewGuid(), Guid.Empty])).IsValid);
    }

    [Fact]
    public void Review_requires_ids_and_known_enums()
    {
        var validator = new SubmitReviewValidator();
        SubmitReviewCommand Valid() => new(Guid.NewGuid(), StudyMode.Quiz, ReviewGrade.Good, Guid.NewGuid(), 0, Learner);
        Assert.True(validator.Validate(Valid()).IsValid);
        Assert.False(validator.Validate(Valid() with { WordId = Guid.Empty }).IsValid);
        Assert.False(validator.Validate(Valid() with { ClientEventId = Guid.Empty }).IsValid);
        Assert.False(validator.Validate(Valid() with { LearnerId = Guid.Empty }).IsValid);
        Assert.False(validator.Validate(Valid() with { Grade = (ReviewGrade)0 }).IsValid);
        Assert.False(validator.Validate(Valid() with { Mode = (StudyMode)9 }).IsValid);
    }

    [Fact]
    public void Reel_count_and_catalog_ids_are_bounded()
    {
        var reels = new GetReelBackgroundsValidator();
        Assert.True(reels.Validate(new GetReelBackgroundsQuery()).IsValid);
        Assert.True(reels.Validate(new GetReelBackgroundsQuery(60)).IsValid);
        Assert.False(reels.Validate(new GetReelBackgroundsQuery(0)).IsValid);
        Assert.False(reels.Validate(new GetReelBackgroundsQuery(61)).IsValid);
        Assert.False(new GetStageSetsValidator().Validate(new GetStageSetsQuery(Guid.Empty)).IsValid);
        Assert.False(new GetSetDetailValidator().Validate(new GetSetDetailQuery(Guid.Empty)).IsValid);
    }

    [Fact]
    public void Import_accepts_up_to_ten_json_files_of_at_most_5_mb()
    {
        var validator = new ImportVocabularyStagesValidator();
        VocabularyImportFile File(string name = "01.json", string? type = "application/json", long size = 10) =>
            new(name, type, size, "{}");
        Assert.True(validator.Validate(new ImportVocabularyStagesCommand([File(), File("02.JSON", null)])).IsValid);
        Assert.True(validator.Validate(new ImportVocabularyStagesCommand([File(type: "application/json; charset=utf-8")])).IsValid);
        Assert.False(validator.Validate(new ImportVocabularyStagesCommand([])).IsValid);
        Assert.False(validator.Validate(new ImportVocabularyStagesCommand(Enumerable.Repeat(File(), 11).ToArray())).IsValid);
        Assert.False(validator.Validate(new ImportVocabularyStagesCommand([File("words.csv")])).IsValid);
        Assert.False(validator.Validate(new ImportVocabularyStagesCommand([File(type: "image/png")])).IsValid);
        Assert.False(validator.Validate(new ImportVocabularyStagesCommand([File(size: 0)])).IsValid);
        Assert.False(validator.Validate(new ImportVocabularyStagesCommand([File(size: 5 * 1024 * 1024 + 1)])).IsValid);
        Assert.False(validator.Validate(new ImportVocabularyStagesCommand([new VocabularyImportFile("a.json", null, 5, "")])).IsValid);
    }

    [Fact]
    public void Study_queue_accepts_folder_or_library_as_the_single_source()
    {
        var validator = new GetStudyQueueValidator();
        Assert.True(validator.Validate(new GetStudyQueueQuery(Learner, null, null, FolderId: Guid.NewGuid())).IsValid);
        Assert.True(validator.Validate(new GetStudyQueueQuery(Learner, null, null, Library: true)).IsValid);
        Assert.False(validator.Validate(new GetStudyQueueQuery(Learner, null, null, FolderId: Guid.NewGuid(), Library: true)).IsValid);
        Assert.False(validator.Validate(new GetStudyQueueQuery(Learner, null, true, Library: true)).IsValid);
        Assert.False(validator.Validate(new GetStudyQueueQuery(Learner, null, null, FolderId: Guid.Empty)).IsValid);
    }

    [Fact]
    public void Folder_names_are_1_to_60_characters()
    {
        var create = new CreateFolderValidator();
        Assert.True(create.Validate(new CreateFolderCommand(new string('a', 60), Learner)).IsValid);
        Assert.False(create.Validate(new CreateFolderCommand(new string('a', 61), Learner)).IsValid);
        Assert.False(create.Validate(new CreateFolderCommand("   ", Learner)).IsValid);
        var update = new UpdateFolderValidator();
        Assert.True(update.Validate(new UpdateFolderCommand(null, 3, 0, Guid.NewGuid(), Learner)).IsValid);
        Assert.False(update.Validate(new UpdateFolderCommand(null, null, 0, Guid.NewGuid(), Learner)).IsValid);
        Assert.False(update.Validate(new UpdateFolderCommand("", null, 0, Guid.NewGuid(), Learner)).IsValid);
        Assert.False(update.Validate(new UpdateFolderCommand(null, 0, 0, Guid.NewGuid(), Learner)).IsValid);
    }

    [Fact]
    public void Custom_words_and_notes_respect_the_contract_limits()
    {
        var validator = new CreateCustomWordValidator();
        CreateCustomWordCommand Word(string headword = "kickoff", string meaning = "khai mạc", string? example = null,
            string? note = null, PartOfSpeech pos = PartOfSpeech.Noun) =>
            new(headword, null, pos, meaning, null, example, null, null, note, 0, Learner);
        Assert.True(validator.Validate(Word(new string('a', 80), new string('b', 200), new string('c', 300), new string('d', 300))).IsValid);
        Assert.False(validator.Validate(Word(headword: new string('a', 81))).IsValid);
        Assert.False(validator.Validate(Word(headword: " ")).IsValid);
        Assert.False(validator.Validate(Word(meaning: new string('b', 201))).IsValid);
        Assert.False(validator.Validate(Word(meaning: "")).IsValid);
        Assert.False(validator.Validate(Word(example: new string('c', 301))).IsValid);
        Assert.False(validator.Validate(Word(note: new string('d', 301))).IsValid);
        Assert.False(validator.Validate(Word(pos: (PartOfSpeech)99)).IsValid);

        var update = new UpdateSavedWordValidator();
        Assert.True(update.Validate(new UpdateSavedWordCommand(Optional<Guid?>.Of(null), default, null, 0, Guid.NewGuid(), Learner)).IsValid);
        Assert.False(update.Validate(new UpdateSavedWordCommand(default, Optional<string?>.Of(new string('x', 301)), null, 0,
            Guid.NewGuid(), Learner)).IsValid);
        Assert.False(update.Validate(new UpdateSavedWordCommand(default, default,
            new CustomWordInput("", null, PartOfSpeech.Noun, "x", null, null, null), 0, Guid.NewGuid(), Learner)).IsValid);
        Assert.False(new SaveWordValidator().Validate(new SaveWordCommand(Guid.NewGuid(), Optional<Guid?>.Of(Guid.Empty),
            default, 0, Learner)).IsValid);
    }

    [Fact]
    public void Saved_word_list_and_sessions_validate_their_filters()
    {
        var words = new GetSavedWordsValidator();
        Assert.True(words.Validate(new GetSavedWordsQuery(Learner, null, true, "x", 1, 100)).IsValid);
        Assert.False(words.Validate(new GetSavedWordsQuery(Learner, Guid.NewGuid(), true, null)).IsValid);
        Assert.False(words.Validate(new GetSavedWordsQuery(Learner, null, null, null, 1, 101)).IsValid);
        Assert.False(words.Validate(new GetSavedWordsQuery(Learner, null, null, null, 0)).IsValid);

        var start = new StartStudySessionValidator();
        Assert.True(start.Validate(new StartStudySessionCommand(StudyMode.Quiz, Library: true, LearnerId: Learner)).IsValid);
        Assert.True(start.Validate(new StartStudySessionCommand(StudyMode.Reel, WordIds: [Guid.NewGuid()], LearnerId: Learner)).IsValid);
        Assert.False(start.Validate(new StartStudySessionCommand(StudyMode.Quiz, LearnerId: Learner)).IsValid);
        Assert.False(start.Validate(new StartStudySessionCommand(StudyMode.Quiz, Guid.NewGuid(), Due: true, LearnerId: Learner)).IsValid);
        Assert.False(start.Validate(new StartStudySessionCommand(StudyMode.Quiz, WordIds: [], LearnerId: Learner)).IsValid);
        Assert.False(start.Validate(new StartStudySessionCommand(StudyMode.Quiz, Due: true, Limit: 101, LearnerId: Learner)).IsValid);
        Assert.False(start.Validate(new StartStudySessionCommand((StudyMode)9, Due: true, LearnerId: Learner)).IsValid);
        Assert.False(start.Validate(new StartStudySessionCommand(StudyMode.Quiz, Due: true, TzOffset: 900, LearnerId: Learner)).IsValid);

        Assert.False(new GetActiveStudySessionValidator().Validate(
            new GetActiveStudySessionQuery(Learner, null, Guid.NewGuid(), null, true)).IsValid);
        Assert.True(new GetActiveStudySessionValidator().Validate(new GetActiveStudySessionQuery(Learner)).IsValid);
        Assert.False(new AnswerStudySessionValidator().Validate(new AnswerStudySessionCommand(Guid.NewGuid(),
            ReviewGrade.Good, Guid.Empty, 0, Guid.NewGuid(), Learner)).IsValid);
    }
}
