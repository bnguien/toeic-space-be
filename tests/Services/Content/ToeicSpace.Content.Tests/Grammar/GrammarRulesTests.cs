using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using ToeicSpace.Content.Application.Interfaces.Services;
using ToeicSpace.Content.Application.Services.Grammar;
using ToeicSpace.Content.Domain.Entities;
using ToeicSpace.Content.Domain.Enums;
using ToeicSpace.Content.Domain.Rules;
using ToeicSpace.Content.Infrastructure.Services.Grammar;
using ToeicSpace.Content.Tests.Vocabulary;
using Xunit;

namespace ToeicSpace.Content.Tests.Grammar;

public sealed class GrammarRulesTests
{
    [Theory]
    [InlineData(9, 8)]
    [InlineData(8, 7)]
    [InlineData(10, 8)]
    [InlineData(11, 9)]
    [InlineData(5, 4)]
    public void Stage_needs_80_percent_of_its_topics_rounded_up(int total, int required)
    {
        Assert.Equal(required, GrammarUnlockRules.RequiredTopics(total));
        Assert.True(GrammarUnlockRules.IsUnlocked(required, total));
        Assert.False(GrammarUnlockRules.IsUnlocked(required - 1, total));
    }

    [Fact]
    public void Empty_stage_never_unlocks() => Assert.False(GrammarUnlockRules.IsUnlocked(0, 0));

    [Fact]
    public void Progress_percent_is_floored_over_three_parts_per_topic()
    {
        Assert.Equal(18, GrammarUnlockRules.ProgressPercent(24, 44));
        Assert.Equal(0, GrammarUnlockRules.ProgressPercent(1, 44));
        Assert.Equal(100, GrammarUnlockRules.ProgressPercent(132, 44));
    }

    [Fact]
    public void Question_result_tracks_first_answer_fix_and_reveal()
    {
        var now = DateTime.UtcNow;
        var row = new GrammarQuestionResult();
        row.Record(false, now);
        row.MarkRevealed(now);
        row.Record(true, now);
        Assert.Equal((false, true, true, true, 1), (row.FirstCorrect!.Value, row.LastCorrect, row.Fixed, row.Revealed, row.WrongCount));
        var right = new GrammarQuestionResult();
        right.Record(true, now);
        right.MarkRevealed(now);
        Assert.False(right.Revealed);
    }

    [Fact]
    public void Known_words_stay_sorted_and_idempotent()
    {
        var state = new GrammarLearnerState();
        Assert.True(state.SetKnownWord("04", "budget", true));
        Assert.True(state.SetKnownWord("04", "asset", true));
        Assert.False(state.SetKnownWord("04", "asset", true));
        Assert.Equal(["asset", "budget"], state.KnownWords["04"]);
        Assert.True(state.SetKnownWord("04", "asset", false));
        Assert.True(state.SetKnownWord("04", "budget", false));
        Assert.False(state.KnownWords.ContainsKey("04"));
    }

    [Fact]
    public void Guest_tickets_verify_and_expire_after_six_hours()
    {
        var clock = new FixedTimeProvider(new DateTimeOffset(2026, 11, 7, 0, 0, 0, TimeSpan.Zero));
        var tokens = new HmacGuestAttemptTokens(Options.Create(new GrammarOptions()), clock,
            NullLogger<HmacGuestAttemptTokens>.Instance);
        var ticket = tokens.Issue(new GuestAttemptRef("04", null, "qc-1"));
        Assert.StartsWith("g.", ticket);
        Assert.Equal(new GuestAttemptRef("04", null, "qc-1"), tokens.Read(ticket));
        Assert.Null(tokens.Read(ticket[..^2] + "AA"));
        Assert.Null(tokens.Read("g.garbage"));
        clock.Advance(TimeSpan.FromHours(6) + TimeSpan.FromSeconds(1));
        Assert.Null(tokens.Read(ticket));
    }

    [Fact]
    public void Public_payload_never_contains_answers()
    {
        var bundle = GrammarContentBuilder.Build(Documents());
        Assert.Equal(44, bundle.Topics.Count);
        Assert.Equal(5, bundle.Stages.Count);
        Assert.Equal(1842, bundle.Questions.Count);
        foreach (var topic in bundle.Topics.Values)
        {
            var json = JsonNode.Parse(topic.PublicJson)!.AsObject();
            AssertNoAnswers(json["exercises"], checkTraps: true);
            AssertNoAnswers(json["vocab"], checkTraps: true);
            AssertNoAnswers(json["knowledge"], checkTraps: false);
        }
        Assert.Contains("toeicTraps", Encoding(bundle.Topics["04"].PublicJson));
        foreach (var stage in bundle.Stages.Values) AssertNoAnswers(stage.PublicTest, checkTraps: true);
        Assert.Equal(36, bundle.Stages["01"].Block.PassMark);
        Assert.Equal(9, bundle.Stages["01"].Topics.Count);
    }

    private static string Encoding(byte[] bytes) => System.Text.Encoding.UTF8.GetString(bytes);

    private static void AssertNoAnswers(JsonNode? node, bool checkTraps)
    {
        switch (node)
        {
            case JsonObject obj:
                foreach (var (key, value) in obj)
                {
                    Assert.False(GrammarContentBuilder.AnswerFields.Contains(key) && (checkTraps || key != "toeicTraps"),
                        $"answer field '{key}' leaked");
                    AssertNoAnswers(value, checkTraps);
                }
                break;
            case JsonArray arr:
                foreach (var item in arr) AssertNoAnswers(item, checkTraps);
                break;
        }
    }

    public static List<GrammarDocument> Documents()
    {
        var bundle = GrammarTestData.Bundle();
        var docs = new List<GrammarDocument>();
        foreach (var topic in (bundle["topics"] as JsonArray)!.OfType<JsonObject>())
        {
            var key = topic["topic"]!.GetValue<string>();
            docs.Add(Doc(GrammarDocumentKind.Knowledge, key, topic["knowledge"]!));
            docs.Add(Doc(GrammarDocumentKind.Vocab, key, topic["vocab"]!));
            docs.Add(Doc(GrammarDocumentKind.Exercises, key, topic["exercises"]!));
        }
        foreach (var stage in (bundle["stages"] as JsonArray)!.OfType<JsonObject>())
            docs.Add(Doc(GrammarDocumentKind.StageTest, stage["stage"]!.GetValue<string>(), stage));
        return docs;
    }

    private static GrammarDocument Doc(GrammarDocumentKind kind, string key, JsonNode node)
    {
        var json = node.ToJsonString(new JsonSerializerOptions { Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping });
        return new GrammarDocument { Kind = kind, Key = key, Content = json, ContentHash = GrammarContentBuilder.Hash(json) };
    }
}
