using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using ToeicSpace.Content.Application.Common.Json;
using ToeicSpace.Content.Application.DTOs.Vocabulary;
using ToeicSpace.Content.Application.DTOs.VocabularyLibrary;
using ToeicSpace.Content.Application.Features.StudySessions.Commands.StartStudySession;
using ToeicSpace.Content.Application.Features.VocabularyCatalog.Queries.GetSetDetail;
using ToeicSpace.Content.Application.Features.VocabularyCatalog.Queries.GetStages;
using ToeicSpace.Content.Application.Features.VocabularyCatalog.Queries.GetStageSets;
using ToeicSpace.Content.Application.Features.VocabularyLibrary.Commands.CreateCustomWord;
using ToeicSpace.Content.Application.Features.VocabularyLibrary.Commands.UpdatePersonalWord;
using ToeicSpace.Content.Application.Features.VocabularyLibrary.Queries.GetLibrary;
using ToeicSpace.Content.Application.Features.VocabularyLibrary.Queries.GetSavedWords;
using ToeicSpace.Content.Application.Features.VocabularyProgress.Commands.SubmitReview;
using ToeicSpace.Content.Application.Features.VocabularyProgress.Queries.GetStudyQueue;
using ToeicSpace.Content.Application.Features.VocabularyQuizzes.Commands.CreateQuiz;
using ToeicSpace.Content.Domain.Enums;
using ToeicSpace.Content.Domain.Exceptions;
using Xunit;

namespace ToeicSpace.Content.Tests.Vocabulary;

/// <summary>"Sửa nghĩa riêng": personal edits stored on the learner's library row and overlaid on read.</summary>
public sealed class PersonalWordTests : IAsyncLifetime
{
    private readonly VocabularyTestContext _ctx = new();
    private Dictionary<string, Guid> _words = [];
    private Guid _lesson;
    private Guid Learner => _ctx.Learner;

    public async Task InitializeAsync()
    {
        await _ctx.InitializeAsync();
        await _ctx.ImportAsync(StageJson.Build("stage-a", 1,
            new StageJson.S("lesson-1", 1, new("alpha"), new("beta"), new("gamma"), new("delta"), new("echo"),
                new("foxtrot"))));
        _words = await _ctx.Db().VocabularyWords.ToDictionaryAsync(x => x.Headword, x => x.Id);
        var stage = (await _ctx.Stages().Handle(new GetStagesQuery(), default))[0].Id;
        _lesson = (await _ctx.StageSets().Handle(new GetStageSetsQuery(stage), default))[0].Id;
    }

    public Task DisposeAsync() => _ctx.DisposeAsync();

    private static Optional<string?> S(string? value) => Optional<string?>.Of(value);

    private Task<SavedWordUpsertResult> Edit(string? word = null, Optional<string?> phonetic = default,
        Optional<string?> pos = default, Optional<string?> meaning = default, Optional<string?> example = default,
        Optional<string?> exampleVi = default, Optional<string?> note = default, bool reset = false,
        Guid? learner = null, Guid? wordId = null) =>
        _ctx.PersonalEdit().Handle(new UpdatePersonalWordCommand(phonetic, pos, meaning, example, exampleVi, note, reset,
            0, wordId ?? _words[word!], learner ?? Learner), default);

    private async Task<WordDto> InSet(string word, Guid? learner = null) =>
        (await _ctx.SetDetail().Handle(new GetSetDetailQuery(_lesson, learner ?? Learner), default)).Words
        .Single(x => x.Headword == word);

    private static async Task<string> Code(Func<Task> action) =>
        (await Assert.ThrowsAsync<ContentRuleException>(action)).Code;

    [Fact]
    public async Task Editing_an_official_word_overrides_only_the_learners_view_and_adds_it_unfiled()
    {
        var first = await Edit("alpha", meaning: S("  nghĩa của tôi "), example: S("My own example."));
        Assert.True(first.Created);
        var word = first.SavedWord.Word;
        Assert.Equal(((Guid?)null, false), (first.SavedWord.FolderId, first.SavedWord.IsCustom));
        Assert.Equal(("nghĩa của tôi", "My own example.", (string?)null), (word.MeaningVi, word.Example, word.Note));
        Assert.Equal(["meaningVi", "example"], word.Personal);
        Assert.Equal(new WordOriginalDto("/alpha/", PartOfSpeech.Noun, "nghĩa alpha", "An example with alpha.", null),
            word.Original);

        // The shared word is untouched.
        var shared = await _ctx.Db().VocabularyWords.SingleAsync(x => x.Id == _words["alpha"]);
        Assert.Equal(("nghĩa alpha", "An example with alpha."), (shared.MeaningVi, shared.Example));

        var second = await Edit("alpha", note: S("ghi chú"));
        Assert.False(second.Created);
        Assert.Equal((first.SavedWord.Id, "ghi chú", "ghi chú"),
            (second.SavedWord.Id, second.SavedWord.Note, second.SavedWord.Word.Note));
        Assert.Equal(["meaningVi", "example"], second.SavedWord.Word.Personal);
        var library = await _ctx.Library().Handle(new GetLibraryQuery(Learner), default);
        Assert.Equal((1, 1), (library.Total, library.Unfiled.WordCount));

        // Set detail: the learner sees the edit; another learner and guests see the shared word.
        Assert.Equal(("nghĩa của tôi", "ghi chú"), ((await InSet("alpha")).MeaningVi, (await InSet("alpha")).Note));
        var beta = await InSet("beta");
        Assert.Equal(((string?)null, (IReadOnlyList<string>?)null, (WordOriginalDto?)null),
            (beta.Note, beta.Personal, beta.Original));
        foreach (var other in new[] { Guid.NewGuid(), Guid.Empty })
        {
            var seen = await InSet("alpha", other);
            Assert.Equal(("nghĩa alpha", (string?)null, (IReadOnlyList<string>?)null, (WordOriginalDto?)null),
                (seen.MeaningVi, seen.Note, seen.Personal, seen.Original));
        }
    }

    [Fact]
    public async Task Blank_or_shared_values_clear_an_override_and_reset_keeps_the_note()
    {
        var edited = (await Edit("alpha", phonetic: S("/ˈæl.fə/"), pos: S("Verb"), meaning: S("riêng"),
            exampleVi: S("Ví dụ riêng"), note: S("n1"))).SavedWord.Word;
        Assert.Equal(["phonetic", "partOfSpeech", "meaningVi", "exampleVi"], edited.Personal);
        Assert.Equal((PartOfSpeech.Verb, PartOfSpeech.Noun), (edited.PartOfSpeech, edited.Original!.PartOfSpeech));

        Assert.Equal(["phonetic", "partOfSpeech", "exampleVi"], (await Edit("alpha", meaning: S(""))).SavedWord.Word.Personal);
        // Equal to the shared value (trimmed) clears; the part of speech name is case-insensitive.
        Assert.Equal(["partOfSpeech", "exampleVi"], (await Edit("alpha", phonetic: S(" /alpha/ "))).SavedWord.Word.Personal);
        Assert.Equal(["exampleVi"], (await Edit("alpha", pos: S("noun"))).SavedWord.Word.Personal);
        Assert.Equal(["partOfSpeech", "exampleVi"], (await Edit("alpha", pos: S("Verb"))).SavedWord.Word.Personal);
        Assert.Equal(["exampleVi"], (await Edit("alpha", pos: S(null))).SavedWord.Word.Personal);
        // The comparison is case-sensitive: a different case is a real edit.
        var cased = (await Edit("alpha", meaning: S("Nghĩa alpha"))).SavedWord.Word;
        Assert.Equal(("Nghĩa alpha", "nghĩa alpha"), (cased.MeaningVi, cased.Original!.MeaningVi));

        var reset = (await Edit("alpha", reset: true)).SavedWord.Word;
        Assert.Equal(("nghĩa alpha", "/alpha/", PartOfSpeech.Noun, "n1"),
            (reset.MeaningVi, reset.Phonetic, reset.PartOfSpeech, reset.Note));
        Assert.Null(reset.Personal);
        Assert.Null(reset.Original);
        Assert.Null((await _ctx.Db().SavedWords.SingleAsync()).PersonalizedAt);

        // Reset runs first, then the sent fields apply (an empty note clears it).
        var again = (await Edit("alpha", reset: true, meaning: S("mới"), note: S(""))).SavedWord;
        Assert.Equal(["meaningVi"], again.Word.Personal!);
        Assert.Equal(((string?)null, (string?)null), (again.Note, again.Word.Note));
        Assert.NotNull((await _ctx.Db().SavedWords.SingleAsync()).PersonalizedAt);
    }

    [Fact]
    public async Task A_custom_word_is_edited_directly_and_never_gets_an_original()
    {
        var custom = await _ctx.CreateCustom().Handle(new CreateCustomWordCommand("synergy", "/x/", PartOfSpeech.Noun,
            "cộng hưởng", null, "An example.", null, null, null, 0, Learner), default);
        var result = await Edit(wordId: custom.Word.Id, meaning: S(" sức mạnh tổng hợp "), phonetic: S(""),
            example: S(""), exampleVi: S("Ví dụ."), note: S("của tôi"));
        Assert.False(result.Created);
        var word = result.SavedWord.Word;
        Assert.Equal(("sức mạnh tổng hợp", "", (string?)null, "Ví dụ.", "của tôi", (IReadOnlyList<string>?)null,
            (WordOriginalDto?)null), (word.MeaningVi, word.Phonetic, word.Example, word.ExampleVi, word.Note,
            word.Personal, word.Original));
        var stored = await _ctx.Db().SavedWords.Include(x => x.Word).SingleAsync(x => x.Id == custom.Id);
        Assert.Equal(("sức mạnh tổng hợp", (string?)null, (DateTime?)null),
            (stored.Word!.MeaningVi, stored.MyMeaningVi, stored.PersonalizedAt));

        Assert.Equal("validation_error", await Code(() => Edit(wordId: custom.Word.Id, meaning: S("  "))));
        await _ctx.CreateCustom().Handle(new CreateCustomWordCommand("synergy", null, PartOfSpeech.Verb, "phối hợp",
            null, null, null, null, null, 0, Learner), default);
        Assert.Equal("custom_word_exists", await Code(() => Edit(wordId: custom.Word.Id, pos: S("Verb"))));
        Assert.Equal("vocabulary_word_not_found",
            await Code(() => Edit(wordId: custom.Word.Id, meaning: S("x"), learner: Guid.NewGuid())));
        Assert.Equal("vocabulary_word_not_found", await Code(() => Edit(wordId: Guid.NewGuid(), meaning: S("x"))));
    }

    [Fact]
    public async Task Edits_show_in_the_queue_sessions_and_library_and_keep_progress()
    {
        await _ctx.Reviewer().Handle(new SubmitReviewCommand(_words["alpha"], StudyMode.Flashcard, ReviewGrade.Good,
            Guid.NewGuid(), 0, Learner), default);
        var saved = (await Edit("alpha", meaning: S("nghĩa riêng"), note: S("nhớ"))).SavedWord;
        Assert.Equal(WordStatus.Learning, saved.Progress.Status); // progress is keyed by word id

        var queue = await _ctx.Queue().Handle(new GetStudyQueueQuery(Learner, _lesson, null), default);
        var item = queue.Single(x => x.Word.Id == _words["alpha"]);
        Assert.Equal(("nghĩa riêng", "nhớ", saved.Id, WordStatus.Learning),
            (item.Word.MeaningVi, item.Word.Note, item.SavedWordId, item.Progress.Status));
        Assert.Equal("nghĩa alpha", item.Word.Original!.MeaningVi);
        Assert.Null(queue.Single(x => x.Word.Id == _words["beta"]).Word.Personal);

        var session = (await _ctx.StartSession().Handle(new StartStudySessionCommand(StudyMode.Reel, _lesson,
            LearnerId: Learner), default)).Session;
        var card = session.Items.Single(x => x.Word.Id == _words["alpha"]);
        Assert.Equal(["meaningVi"], card.Word.Personal!);
        Assert.Equal("nghĩa riêng", card.Word.MeaningVi);

        var library = await _ctx.SavedWords().Handle(new GetSavedWordsQuery(Learner, null, null, "riêng"), default);
        Assert.Equal("nghĩa riêng", Assert.Single(library.Items).Word.MeaningVi); // search also matches the edit
    }

    [Fact]
    public async Task Quiz_uses_personal_meanings_and_keeps_options_distinct()
    {
        await Edit("alpha", meaning: S("trái cây"));
        await Edit("beta", meaning: S("Trái  cây")); // same as the prompt once normalised: never a distractor
        await Edit("gamma", meaning: S("nghĩa delta")); // gamma and delta now share a meaning: at most one of them
        for (var i = 0; i < 30; i++)
        {
            var question = Assert.Single(await _ctx.Quiz().Handle(new CreateQuizCommand([_words["alpha"]], Learner), default));
            Assert.Equal(("trái cây", PartOfSpeech.Noun), (question.Prompt, question.PartOfSpeech));
            var headwords = question.Options.Select(x => x.Headword).ToArray();
            Assert.Equal(4, headwords.Select(x => x.ToLowerInvariant()).Distinct().Count());
            Assert.Contains("alpha", headwords);
            Assert.DoesNotContain("beta", headwords);
            Assert.False(headwords.Contains("gamma") && headwords.Contains("delta"));
        }
        var guest = Assert.Single(await _ctx.Quiz().Handle(new CreateQuizCommand([_words["alpha"]]), default));
        Assert.Equal("nghĩa alpha", guest.Prompt);

        await Edit("alpha", pos: S("Verb"));
        Assert.Equal(PartOfSpeech.Verb,
            Assert.Single(await _ctx.Quiz().Handle(new CreateQuizCommand([_words["alpha"]], Learner), default)).PartOfSpeech);
    }

    [Fact]
    public void Personal_edit_requests_are_validated_and_bind_absent_versus_blank()
    {
        var validator = new UpdatePersonalWordValidator();
        var word = Guid.NewGuid();
        UpdatePersonalWordCommand Cmd(Optional<string?> phonetic = default, Optional<string?> pos = default,
            Optional<string?> meaning = default, Optional<string?> example = default, Optional<string?> note = default) =>
            new(phonetic, pos, meaning, example, default, note, false, 0, word, Learner);
        Assert.True(validator.Validate(Cmd(S(new string('p', 100)), S(""), S(new string('m', 200)),
            S(new string('e', 300)), S(new string('n', 1000)))).IsValid);
        Assert.True(validator.Validate(Cmd(pos: S("adverb"))).IsValid);
        Assert.False(validator.Validate(Cmd(phonetic: S(new string('p', 101)))).IsValid);
        Assert.False(validator.Validate(Cmd(meaning: S(new string('m', 201)))).IsValid);
        Assert.False(validator.Validate(Cmd(example: S(new string('e', 301)))).IsValid);
        Assert.False(validator.Validate(Cmd(note: S(new string('n', 1001)))).IsValid);
        Assert.False(validator.Validate(Cmd(pos: S("banana"))).IsValid);
        Assert.False(validator.Validate(Cmd(pos: S("1"))).IsValid);
        Assert.False(validator.Validate(Cmd() with { WordId = Guid.Empty }).IsValid);

        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };
        var body = JsonSerializer.Deserialize<UpdatePersonalWordCommand>(
            """{ "meaningVi": "", "partOfSpeech": "Verb", "note": null, "reset": true }""", options)!;
        Assert.Equal((true, "", true, "Verb", true, (string?)null, false, true),
            (body.MeaningVi.IsSet, body.MeaningVi.Value, body.PartOfSpeech.IsSet, body.PartOfSpeech.Value, body.Note.IsSet,
                body.Note.Value, body.Phonetic.IsSet, body.Reset));
    }

    [Fact]
    public void Word_dto_serializes_the_contract_shape()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };
        var edited = new WordDto(Guid.Empty, "alpha", "/a/", PartOfSpeech.Verb, "riêng", null, null, null, "B1", [],
            "ghi chú", ["partOfSpeech", "meaningVi"],
            new WordOriginalDto("/alpha/", PartOfSpeech.Noun, "nghĩa alpha", "Ex.", null));
        using var json = JsonDocument.Parse(JsonSerializer.Serialize(edited, options));
        var root = json.RootElement;
        Assert.Equal(("Verb", "ghi chú", "partOfSpeech,meaningVi", "Noun", "nghĩa alpha"),
            (root.GetProperty("partOfSpeech").GetString(), root.GetProperty("note").GetString(),
                string.Join(',', root.GetProperty("personal").EnumerateArray().Select(x => x.GetString())),
                root.GetProperty("original").GetProperty("partOfSpeech").GetString(),
                root.GetProperty("original").GetProperty("meaningVi").GetString()));
        Assert.Equal(["phonetic", "partOfSpeech", "meaningVi", "example", "exampleVi"],
            root.GetProperty("original").EnumerateObject().Select(x => x.Name));

        using var plain = JsonDocument.Parse(JsonSerializer.Serialize(edited with { Note = null, Personal = null,
            Original = null }, options));
        Assert.Equal((JsonValueKind.Null, JsonValueKind.Null, JsonValueKind.Null),
            (plain.RootElement.GetProperty("note").ValueKind, plain.RootElement.GetProperty("personal").ValueKind,
                plain.RootElement.GetProperty("original").ValueKind));
    }
}
