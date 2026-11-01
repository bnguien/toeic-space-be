using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using ToeicSpace.Content.Application.Features.VocabularyCatalog.Queries.GetSetDetail;
using ToeicSpace.Content.Application.Features.VocabularyCatalog.Queries.GetStages;
using ToeicSpace.Content.Application.Features.VocabularyCatalog.Queries.GetStageSets;
using ToeicSpace.Content.Domain.Entities;
using ToeicSpace.Content.Domain.Enums;
using ToeicSpace.Content.Domain.Exceptions;
using Xunit;

namespace ToeicSpace.Content.Tests.Vocabulary;

public sealed class VocabularyImportTests : IAsyncLifetime
{
    private readonly VocabularyTestContext _ctx = new();
    private static readonly string StageFile =
        File.ReadAllText(VocabularyTestContext.RepositoryFile("data/vocabulary/01-toeic-roadmap-250.json"));

    public Task InitializeAsync() => _ctx.InitializeAsync();
    public Task DisposeAsync() => _ctx.DisposeAsync();

    [Fact]
    public async Task Imports_the_real_first_stage_and_reimport_is_unchanged()
    {
        var source = JsonNode.Parse(StageFile)!;
        var sets = source["sets"]!.AsArray();
        var wordCount = sets.Sum(s => s!["words"]!.AsArray().Count);

        var first = Assert.Single((await _ctx.ImportAsync(StageFile)).Stages);
        Assert.Equal(("toeic-roadmap-250", sets.Count, 0, wordCount, 0, 0),
            (first.Slug, first.SetsCreated, first.SetsUpdated, first.WordsCreated, first.WordsUpdated, first.Unchanged));

        var db = _ctx.Db();
        Assert.Equal(1, await db.VocabularyStages.CountAsync());
        Assert.Equal(sets.Count, await db.VocabularySets.CountAsync());
        Assert.Equal(wordCount, await db.VocabularyWords.CountAsync());
        Assert.Equal(wordCount, await db.VocabularySetWords.CountAsync());
        var stage = await db.VocabularyStages.SingleAsync();
        Assert.Equal((1, 250), (stage.Order, stage.ToeicTarget));

        var again = Assert.Single((await _ctx.ImportAsync(StageFile)).Stages);
        Assert.Equal((0, 0, 0, 0, sets.Count + wordCount),
            (again.SetsCreated, again.SetsUpdated, again.WordsCreated, again.WordsUpdated, again.Unchanged));

        // The first lesson keeps the file's word order.
        var lesson = await db.VocabularySets.AsNoTracking().SingleAsync(x => x.Order == 1);
        var detail = await _ctx.SetDetail().Handle(new GetSetDetailQuery(lesson.Id), default);
        Assert.Equal(sets[0]!["words"]!.AsArray().Select(w => (string)w!["word"]!), detail.Words.Select(w => w.Headword));
        var culture = detail.Words.First(w => w.Headword == "culture");
        Assert.Equal((PartOfSpeech.Noun, "văn hóa"), (culture.PartOfSpeech, culture.MeaningVi));
    }

    [Fact]
    public async Task Imports_the_whole_roadmap_in_one_call()
    {
        var files = Directory.GetFiles(VocabularyTestContext.RepositoryFile("data/vocabulary"), "0*-toeic-roadmap-*.json")
            .Order().Select(File.ReadAllText).ToArray();
        var result = await _ctx.ImportAsync(files);
        Assert.Equal(5, result.Stages.Count);
        Assert.Equal([18, 16, 14, 26, 26], result.Stages.Select(x => x.SetsCreated));
        Assert.Equal(4954, result.Stages.Sum(x => x.WordsCreated));
        var db = _ctx.Db();
        Assert.Equal((100, 4954), (await db.VocabularySets.CountAsync(), await db.VocabularyWords.CountAsync()));
        var stages = await _ctx.Stages().Handle(new GetStagesQuery(), default);
        Assert.Equal([1, 2, 3, 4, 5], stages.Select(x => x.Order));
        Assert.Equal(["Khởi động", "Phản xạ", "Tăng tốc", "Bứt phá", "Về đích"], stages.Select(x => x.Title));
        Assert.Equal((18, 898, 250), (stages[0].SetCount, stages[0].WordCount, stages[0].ToeicTarget));

        // Lessons are 50 words each, the last one shorter, shown as "first → last".
        var lessons = await _ctx.StageSets().Handle(new GetStageSetsQuery(stages[0].Id), default);
        Assert.Equal(["bai-01", "bai-02"], lessons.Take(2).Select(x => x.Slug));
        Assert.Equal(("Bài 01", 50, "culture", "always"),
            (lessons[0].Title, lessons[0].WordCount, lessons[0].PreviewWords[0], lessons[0].LastWord));
        Assert.Equal(48, lessons[^1].WordCount);
    }

    [Fact]
    public async Task Updated_meaning_is_applied_and_counted_once()
    {
        await _ctx.ImportAsync(StageFile);
        var source = JsonNode.Parse(StageFile)!;
        var word = source["sets"]![0]!["words"]![0]!;
        word["meaningVi"] = "văn hoá (đã sửa)";
        word["synonyms"] = new JsonArray("civilization");

        var result = Assert.Single((await _ctx.ImportAsync(source.ToJsonString())).Stages);
        Assert.Equal((0, 0, 0, 1), (result.SetsCreated, result.SetsUpdated, result.WordsCreated, result.WordsUpdated));

        var stored = await _ctx.Db().VocabularyWords.AsNoTracking().SingleAsync(x => x.NormalizedHeadword == "culture");
        Assert.Equal("văn hoá (đã sửa)", stored.MeaningVi);
        Assert.Equal(["civilization"], stored.Synonyms);
    }

    [Fact]
    public async Task Words_with_progress_are_never_deleted_and_removed_sets_are_hidden()
    {
        var original = StageJson.Build("stage-a", 1,
            new StageJson.S("lesson-1", 1, new("alpha"), new("beta"), new("gamma")),
            new StageJson.S("lesson-2", 2, new("delta"), new("epsilon")));
        await _ctx.ImportAsync(original);
        var db = _ctx.Db();
        var delta = await db.VocabularyWords.SingleAsync(x => x.NormalizedHeadword == "delta");
        db.LearnerWordProgress.Add(new LearnerWordProgress
        {
            LearnerId = _ctx.Learner, WordId = delta.Id, IntervalDays = 6, Repetitions = 2,
            DueOn = new DateOnly(2026, 11, 7)
        });
        await db.SaveChangesAsync();

        // lesson-2 disappears, beta leaves lesson-1 and the remaining words are reordered.
        var changed = StageJson.Build("stage-a", 1,
            new StageJson.S("lesson-1", 1, new("gamma"), new("alpha"), new("zeta")));
        var result = Assert.Single((await _ctx.ImportAsync(changed)).Stages);
        Assert.Equal((0, 2, 1, 0), (result.SetsCreated, result.SetsUpdated, result.WordsCreated, result.WordsUpdated));

        var check = _ctx.Db();
        Assert.Equal(6, await check.VocabularyWords.CountAsync());
        Assert.True(await check.LearnerWordProgress.AnyAsync(x => x.WordId == delta.Id));
        var hidden = await check.VocabularySets.SingleAsync(x => x.Slug == "lesson-2");
        Assert.False(hidden.IsActive);
        Assert.Equal(2, await check.VocabularySetWords.CountAsync(x => x.SetId == hidden.Id));

        var stageId = (await check.VocabularyStages.SingleAsync()).Id;
        var visible = await _ctx.StageSets().Handle(new GetStageSetsQuery(stageId), default);
        var lesson = Assert.Single(visible);
        Assert.Equal(["gamma", "alpha", "zeta"], lesson.PreviewWords);
        Assert.Equal("zeta", lesson.LastWord);
        Assert.Equal("vocabulary_set_not_found", (await Assert.ThrowsAsync<ContentRuleException>(() =>
            _ctx.SetDetail().Handle(new GetSetDetailQuery(hidden.Id), default))).Code);

        // Bringing the lesson back reactivates it.
        var restored = Assert.Single((await _ctx.ImportAsync(original)).Stages);
        Assert.Equal(2, restored.SetsUpdated);
        Assert.True((await _ctx.Db().VocabularySets.SingleAsync(x => x.Slug == "lesson-2")).IsActive);
    }

    [Fact]
    public async Task Same_word_in_two_stages_is_stored_once()
    {
        var result = await _ctx.ImportAsync(
            StageJson.Build("stage-a", 1, new StageJson.S("lesson-1", 1, new("Report"), new("report", "verb"))),
            StageJson.Build("stage-b", 2, new StageJson.S("lesson-1", 1, new("report  "), new("budget"))));
        Assert.Equal([2, 1], result.Stages.Select(x => x.WordsCreated));
        Assert.Equal(3, await _ctx.Db().VocabularyWords.CountAsync());
        Assert.Equal(2, await _ctx.Db().VocabularySets.CountAsync());
    }

    [Fact]
    public async Task Invalid_files_are_rejected_before_anything_is_written()
    {
        var badPos = StageJson.Build("stage-a", 1, new StageJson.S("lesson-1", 1, new StageJson.W("alpha", "noun-ish")));
        var good = StageJson.Build("stage-b", 2, new StageJson.S("lesson-1", 1, new StageJson.W("alpha")));
        Assert.Equal("invalid_import_file", (await Assert.ThrowsAsync<ContentRuleException>(() =>
            _ctx.ImportAsync(good, badPos))).Code);
        Assert.Equal("invalid_import_file", (await Assert.ThrowsAsync<ContentRuleException>(() =>
            _ctx.ImportAsync("{ not json"))).Code);
        Assert.Equal("invalid_import_file", (await Assert.ThrowsAsync<ContentRuleException>(() =>
            _ctx.ImportAsync(good, good))).Code);
        Assert.Equal("invalid_import_file", (await Assert.ThrowsAsync<ContentRuleException>(() =>
            _ctx.ImportAsync(StageJson.Build("stage-c", 1,
                new StageJson.S("lesson-1", 1, new("alpha"), new("ALPHA")))))).Code);
        Assert.Equal(0, await _ctx.Db().VocabularyStages.CountAsync());
    }
}
