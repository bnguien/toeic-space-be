using ToeicSpace.Content.Application.Features.VocabularyCatalog.Queries.GetSetDetail;
using ToeicSpace.Content.Application.Features.VocabularyCatalog.Queries.GetStages;
using ToeicSpace.Content.Application.Features.VocabularyCatalog.Queries.GetStageSets;
using ToeicSpace.Content.Domain.Enums;
using ToeicSpace.Content.Domain.Exceptions;
using Xunit;

namespace ToeicSpace.Content.Tests.Vocabulary;

public sealed class VocabularyCatalogTests : IAsyncLifetime
{
    private readonly VocabularyTestContext _ctx = new();

    public async Task InitializeAsync()
    {
        await _ctx.InitializeAsync();
        // Files and sets are deliberately out of order.
        await _ctx.ImportAsync(
            StageJson.Build("stage-b", 2,
                new StageJson.S("b-lesson-1", 1, new("budget"), new("invoice"))),
            StageJson.Build("stage-a", 1,
                new StageJson.S("a-lesson-2", 2, new("delta", "verb"), new("echo")),
                new StageJson.S("a-lesson-1", 1, new("alpha"), new("beta", "adjective"), new("gamma"),
                    new("omega"), new("budget"))));
    }

    public Task DisposeAsync() => _ctx.DisposeAsync();

    [Fact]
    public async Task Stages_are_ordered_with_set_and_distinct_word_counts()
    {
        var stages = await _ctx.Stages().Handle(new GetStagesQuery(), default);
        Assert.Equal(["stage-a", "stage-b"], stages.Select(x => x.Slug));
        Assert.Equal((2, 7, 100), (stages[0].SetCount, stages[0].WordCount, stages[0].ToeicTarget));
        Assert.Equal((1, 2), (stages[1].SetCount, stages[1].WordCount));
        Assert.Equal("Stage stage-a", stages[0].Title);
    }

    [Fact]
    public async Task Stage_sets_are_ordered_with_three_preview_words()
    {
        var stageA = (await _ctx.Stages().Handle(new GetStagesQuery(), default))[0];
        var sets = await _ctx.StageSets().Handle(new GetStageSetsQuery(stageA.Id), default);
        Assert.Equal(["a-lesson-1", "a-lesson-2"], sets.Select(x => x.Slug));
        Assert.Equal(["alpha", "beta", "gamma"], sets[0].PreviewWords);
        Assert.Equal((5, "budget"), (sets[0].WordCount, sets[0].LastWord));
        Assert.Equal(["delta", "echo"], sets[1].PreviewWords);
        Assert.Equal("echo", sets[1].LastWord);
        Assert.All(sets, x => Assert.Equal(stageA.Id, x.StageId));

        Assert.Equal("vocabulary_stage_not_found", (await Assert.ThrowsAsync<ContentRuleException>(() =>
            _ctx.StageSets().Handle(new GetStageSetsQuery(Guid.NewGuid()), default))).Code);
    }

    [Fact]
    public async Task Set_detail_lists_words_in_order_and_links_previous_and_next_across_stages()
    {
        var stages = await _ctx.Stages().Handle(new GetStagesQuery(), default);
        var a = await _ctx.StageSets().Handle(new GetStageSetsQuery(stages[0].Id), default);
        var b = await _ctx.StageSets().Handle(new GetStageSetsQuery(stages[1].Id), default);

        var first = await _ctx.SetDetail().Handle(new GetSetDetailQuery(a[0].Id), default);
        Assert.Equal((null, a[1].Id), (first.PreviousSetId, first.NextSetId));
        Assert.Equal(("stage-a", "Stage stage-a"), (first.StageSlug, first.StageTitle));
        Assert.Equal(["alpha", "beta", "gamma", "omega", "budget"], first.Words.Select(x => x.Headword));
        var beta = first.Words[1];
        Assert.Equal((PartOfSpeech.Adjective, "nghĩa beta", "/beta/", "B1"),
            (beta.PartOfSpeech, beta.MeaningVi, beta.Phonetic, beta.Cefr));

        var second = await _ctx.SetDetail().Handle(new GetSetDetailQuery(a[1].Id), default);
        Assert.Equal((a[0].Id, b[0].Id), (second.PreviousSetId, second.NextSetId));
        var last = await _ctx.SetDetail().Handle(new GetSetDetailQuery(b[0].Id), default);
        Assert.Equal((a[1].Id, null), (last.PreviousSetId, last.NextSetId));
        // The same headword in two stages is one word.
        Assert.Equal(first.Words[4].Id, last.Words[0].Id);

        Assert.Equal("vocabulary_set_not_found", (await Assert.ThrowsAsync<ContentRuleException>(() =>
            _ctx.SetDetail().Handle(new GetSetDetailQuery(Guid.NewGuid()), default))).Code);
    }
}
