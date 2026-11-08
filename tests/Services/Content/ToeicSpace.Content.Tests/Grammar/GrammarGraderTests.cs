using System.Text.Json.Nodes;
using ToeicSpace.Content.Domain.Rules;
using Xunit;

namespace ToeicSpace.Content.Tests.Grammar;

public sealed class GrammarGraderTests
{
    private static JsonObject Q(string json) => (JsonObject)JsonNode.Parse(json)!;
    private static JsonNode? V(string json) => JsonNode.Parse(json);

    [Theory]
    [InlineData("  The Report  is READY.  ", "the report is ready")]
    [InlineData("It’s “fine” !", "it's \"fine\"")]
    [InlineData("a 　b , c ?!", "a b, c")]
    [InlineData("done...", "done")]
    [InlineData("", "")]
    public void Normalize_follows_the_javascript_rules(string input, string expected) =>
        Assert.Equal(expected, GrammarGrader.NormalizeText(input));

    [Fact]
    public void NormalizeOrder_drops_commas() =>
        Assert.Equal("however we left", GrammarGrader.NormalizeOrder("However, we left."));

    [Fact]
    public void Letter_kinds_are_exact_and_case_sensitive()
    {
        var q = Q("""{"kind":"mcq5","answer":"B"}""");
        Assert.True(GrammarGrader.GradeQuestion(q, V("\"B\"")));
        Assert.False(GrammarGrader.GradeQuestion(q, V("\"b\"")));
        Assert.False(GrammarGrader.GradeQuestion(q, V("2")));
        Assert.True(GrammarGrader.GradeQuestion(Q("""{"kind":"pair990","answer":"D"}"""), V("\"D\"")));
        Assert.True(GrammarGrader.GradeClozeBlank(Q("""{"answer":"C"}"""), V("\"C\"")));
    }

    [Fact]
    public void Wordform_uses_accepted_answers_and_normalisation()
    {
        var q = Q("""{"kind":"wordform","answer":"approval","acceptedAnswers":["approval","the approval"]}""");
        Assert.True(GrammarGrader.GradeQuestion(q, V("\" The Approval. \"")));
        Assert.False(GrammarGrader.GradeQuestion(q, V("\"approve\"")));
        Assert.False(GrammarGrader.GradeQuestion(Q("""{"kind":"wordform","answer":"x","acceptedAnswers":[]}"""), V("\"x\"")));
    }

    [Fact]
    public void Transform_accepts_the_whole_sentence_or_the_part_after_the_hint()
    {
        var q = Q("""{"kind":"transform","hint":"The report","answer":"The report was approved by Ms. Lee."}""");
        Assert.True(GrammarGrader.GradeQuestion(q, V("\"was approved by Ms. Lee\"")));
        Assert.True(GrammarGrader.GradeQuestion(q, V("\"the report was approved by ms. lee\"")));
        Assert.False(GrammarGrader.GradeQuestion(q, V("\"   \"")));
        Assert.False(GrammarGrader.GradeQuestion(q, V("{\"a\":1}")));
    }

    [Fact]
    public void Order_accepts_a_sentence_or_unique_piece_indexes()
    {
        var q = Q("""{"kind":"order","pieces":["we","left","early"],"answer":"We left early."}""");
        Assert.True(GrammarGrader.GradeQuestion(q, V("[0,1,2]")));
        Assert.True(GrammarGrader.GradeQuestion(q, V("\"we, left early\"")));
        Assert.False(GrammarGrader.GradeQuestion(q, V("[0,0,2]")));
        Assert.False(GrammarGrader.GradeQuestion(q, V("[0,1,5]")));
        Assert.False(GrammarGrader.GradeQuestion(q, V("[0,1.5,2]")));
        Assert.False(GrammarGrader.GradeQuestion(q, V("[]")));
    }

    [Fact]
    public void Error_needs_the_part_and_the_correction()
    {
        var q = Q("""{"kind":"error","answer":"B","correction":"goes","acceptedAnswers":["is going"]}""");
        Assert.True(GrammarGrader.GradeQuestion(q, V("""{"part":"B","correction":"Goes"}""")));
        Assert.True(GrammarGrader.GradeQuestion(q, V("""{"part":"B","correction":"is going"}""")));
        Assert.False(GrammarGrader.GradeQuestion(q, V("""{"part":"A","correction":"goes"}""")));
        Assert.False(GrammarGrader.GradeQuestion(q, V("""{"part":"B","correction":""}""")));
        Assert.False(GrammarGrader.GradeQuestion(q, V("\"B\"")));
    }

    [Fact]
    public void Parse_compares_labels_in_order()
    {
        var q = Q("""{"kind":"parse","answer":["S","V","O"]}""");
        Assert.True(GrammarGrader.GradeQuestion(q, V("""["S","V","O"]""")));
        Assert.False(GrammarGrader.GradeQuestion(q, V("""["S","V"]""")));
        Assert.False(GrammarGrader.GradeQuestion(q, V("""["S","O","V"]""")));
    }

    [Fact]
    public void Match_accepts_an_object_or_letters()
    {
        var q = Q("""{"kind":"match","left":["a","b"],"answer":["1-C","2-A"]}""");
        Assert.True(GrammarGrader.GradeQuestion(q, V("""{"1":"C","2":"A"}""")));
        Assert.True(GrammarGrader.GradeQuestion(q, V("""["C","A"]""")));
        Assert.False(GrammarGrader.GradeQuestion(q, V("""{"1":"C"}""")));
        Assert.False(GrammarGrader.GradeQuestion(q, V("""["A","C"]""")));
    }

    [Fact]
    public void Judge_needs_verdict_and_reason()
    {
        var q = Q("""{"kind":"judge","answer":"incorrect","correctReason":"B"}""");
        Assert.True(GrammarGrader.GradeQuestion(q, V("""{"verdict":"incorrect","reason":"B"}""")));
        Assert.False(GrammarGrader.GradeQuestion(q, V("""{"verdict":"incorrect","reason":"A"}""")));
        Assert.False(GrammarGrader.GradeQuestion(q, V("""{"verdict":"correct","reason":"B"}""")));
    }

    [Theory]
    [InlineData("null", true)]
    [InlineData("\"  \"", true)]
    [InlineData("[]", true)]
    [InlineData("{}", true)]
    [InlineData("""{"part":"","correction":" "}""", true)]
    [InlineData("\"A\"", false)]
    [InlineData("0", false)]
    [InlineData("""{"part":"A"}""", false)]
    public void Blank_answers_are_detected(string json, bool blank) =>
        Assert.Equal(blank, GrammarGrader.IsBlank(JsonNode.Parse(json)));

    [Fact]
    public void Missing_value_is_blank_and_wrong()
    {
        Assert.True(GrammarGrader.IsBlank(null));
        Assert.False(GrammarGrader.GradeQuestion(Q("""{"kind":"mcq5","answer":"A"}"""), null));
    }

    [RequiresDataFact("data/grammar/grammar-content.json.gz")]
    public void Every_content_answer_grades_correct()
    {
        var bundle = GrammarTestData.Bundle();
        var failures = new List<string>();
        var count = 0;
        foreach (var (q, blank) in GrammarTestData.Questions(bundle))
        {
            count++;
            if (!GrammarGrader.Grade(q, GrammarGrader.CanonicalAnswer(q, blank), blank)) failures.Add(q["id"]!.GetValue<string>());
        }
        Assert.Equal(1842, count);
        Assert.Empty(failures);
    }

    [RequiresDataFact("data/grammar/grammar-content.json.gz")]
    public void All_ten_question_kinds_are_present_in_the_content()
    {
        var kinds = GrammarTestData.Questions(GrammarTestData.Bundle())
            .Select(x => x.IsClozeBlank ? "cloze6" : x.Question["kind"]!.GetValue<string>()).ToHashSet();
        Assert.Equal(["cloze6", "error", "judge", "match", "mcq5", "order", "pair990", "parse", "transform", "wordform"],
            kinds.Order());
    }
}
