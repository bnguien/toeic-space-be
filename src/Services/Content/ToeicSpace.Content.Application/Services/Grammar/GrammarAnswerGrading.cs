using System.Text.Json;
using System.Text.Json.Nodes;
using ToeicSpace.Content.Application.DTOs.Grammar;
using ToeicSpace.Content.Application.Models.Grammar;
using ToeicSpace.Content.Domain.Exceptions;
using ToeicSpace.Content.Domain.Rules;

namespace ToeicSpace.Content.Application.Services.Grammar;

/// <summary>Grades a submitted block or a single answer and shapes the per-question results.</summary>
public static class GrammarAnswerGrading
{
    public const int MaxAnswers = 150;
    public const int MaxValueChars = 600;

    public sealed record BlockGrade(Dictionary<string, QuestionResultDto> Results,
        List<(GrammarQuestionInfo Question, bool Correct)> Answered, int Score, Dictionary<string, bool> Correctness);

    public static BlockGrade GradeBlock(GrammarBlock block, JsonObject? answers)
    {
        answers ??= [];
        if (answers.Count > MaxAnswers) throw GrammarErrors.Invalid("Too many answers.");
        var unknown = answers.Select(a => a.Key).Where(k => !block.Ids.Contains(k)).Order(StringComparer.Ordinal).Take(5).ToList();
        if (unknown.Count > 0)
            throw GrammarErrors.Invalid($"Questions not in block {block.Key}: {string.Join(", ", unknown)}");
        foreach (var (_, value) in answers) CheckValue(value);

        var results = new Dictionary<string, QuestionResultDto>(StringComparer.Ordinal);
        var correctness = new Dictionary<string, bool>(StringComparer.Ordinal);
        var answered = new List<(GrammarQuestionInfo, bool)>();
        var score = 0;
        foreach (var question in block.Questions)
        {
            var value = answers[question.Id];
            var correct = GrammarGrader.Grade(question.Question, value, question.IsClozeBlank);
            if (correct) score++;
            results[question.Id] = ResultOf(question, correct);
            correctness[question.Id] = correct;
            if (!GrammarGrader.IsBlank(value)) answered.Add((question, correct));
        }
        return new BlockGrade(results, answered, score, correctness);
    }

    public static QuestionResultDto ResultOf(GrammarQuestionInfo question, bool correct) =>
        correct ? new QuestionResultDto(true, new QuestionExplanationDto(question.Signal, question.Rule))
            : new QuestionResultDto(false, null);

    public static void CheckValue(JsonNode? value)
    {
        if (value is not null && value.ToJsonString(GrammarContentBuilder.PublicJson).Length > MaxValueChars)
            throw GrammarErrors.Invalid("Answer is too long.");
    }

    public static string Serialize(JsonObject? answers) =>
        (answers ?? []).ToJsonString(GrammarContentBuilder.PublicJson);

    public static JsonSerializerOptions JsonOptions => GrammarContentBuilder.PublicJson;
}
