using Microsoft.EntityFrameworkCore;
using ToeicSpace.Content.Application.Features.VocabularyQuizzes.Commands.CreateQuiz;
using ToeicSpace.Content.Domain.Enums;
using ToeicSpace.Content.Domain.Exceptions;
using Xunit;

namespace ToeicSpace.Content.Tests.Vocabulary;

public sealed class QuizTests : IAsyncLifetime
{
    private readonly VocabularyTestContext _ctx = new();
    private Dictionary<string, Guid> _words = [];

    public async Task InitializeAsync()
    {
        await _ctx.InitializeAsync();
        await _ctx.ImportAsync(
            StageJson.Build("stage-a", 1,
                new StageJson.S("lesson-1", 1, new("bike", Meaning: "xe đạp"), new("bicycle", Meaning: "Xe đạp "),
                    new("desk"), new("chair"), new("lamp")),
                new StageJson.S("lesson-2", 2, new("window"), new("door"), new("run", "verb"), new("walk", "verb"),
                    new("quickly", "adverb"))),
            StageJson.Build("stage-b", 2,
                new StageJson.S("lesson-1", 1, new("invoice"), new("budget"), new("revenue"), new("audit"))));
        _words = await _ctx.Db().VocabularyWords.ToDictionaryAsync(x => x.Headword, x => x.Id);
    }

    public Task DisposeAsync() => _ctx.DisposeAsync();

    [Fact]
    public async Task Options_are_four_unique_words_of_the_same_part_of_speech_from_the_same_stage()
    {
        var stageA = new[] { "bike", "bicycle", "desk", "chair", "lamp", "window", "door" };
        for (var i = 0; i < 30; i++)
        {
            var question = Assert.Single(await _ctx.Quiz().Handle(new CreateQuizCommand([_words["bike"]]), default));
            Assert.Equal((_words["bike"], "xe đạp", PartOfSpeech.Noun),
                (question.WordId, question.Prompt, question.PartOfSpeech));
            Assert.Equal(4, question.Options.Count);
            Assert.Equal(4, question.Options.Select(x => x.WordId).Distinct().Count());
            Assert.Equal(4, question.Options.Select(x => x.Headword).Distinct().Count());
            Assert.Contains(question.Options, x => x.WordId == _words["bike"] && x.Headword == "bike");
            // bicycle has the same meaning as the prompt, so it would be a second correct answer.
            Assert.DoesNotContain(question.Options, x => x.Headword == "bicycle");
            Assert.All(question.Options, x => Assert.Contains(x.Headword, stageA));
        }
    }

    [Fact]
    public async Task Falls_back_to_other_parts_of_speech_from_the_same_stage()
    {
        for (var i = 0; i < 20; i++)
        {
            var question = Assert.Single(await _ctx.Quiz().Handle(new CreateQuizCommand([_words["run"]]), default));
            var headwords = question.Options.Select(x => x.Headword).ToArray();
            Assert.Equal(4, headwords.Distinct().Count());
            Assert.Contains("run", headwords);
            Assert.Contains("walk", headwords); // the only other verb is always used first
            Assert.DoesNotContain(headwords, x => x is "invoice" or "budget" or "revenue" or "audit");
        }
    }

    [Fact]
    public async Task Questions_keep_the_request_order_and_unknown_words_fail()
    {
        var ids = new[] { _words["audit"], _words["desk"], _words["quickly"] };
        var questions = await _ctx.Quiz().Handle(new CreateQuizCommand(ids), default);
        Assert.Equal(ids, questions.Select(x => x.WordId));
        Assert.All(questions[0].Options, x => Assert.Contains(x.Headword, new[] { "invoice", "budget", "revenue", "audit" }));

        Assert.Equal("vocabulary_word_not_found", (await Assert.ThrowsAsync<ContentRuleException>(() =>
            _ctx.Quiz().Handle(new CreateQuizCommand([_words["desk"], Guid.NewGuid()]), default))).Code);
    }
}
