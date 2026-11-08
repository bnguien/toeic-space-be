using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using ToeicSpace.Content.Application.Models.Grammar;
using ToeicSpace.Content.Domain.Entities;
using ToeicSpace.Content.Domain.Enums;
using ToeicSpace.Content.Domain.Exceptions;

namespace ToeicSpace.Content.Application.Services.Grammar;

/// <summary>
/// Builds the in-memory <see cref="GrammarContentBundle"/> from the stored documents: question index, blocks, unit and
/// trap names, and the public payloads with every answer field removed at every depth (cloze blanks included).
/// In knowledge documents <c>toeicTraps</c> is a teaching block and is kept.
/// </summary>
public static class GrammarContentBuilder
{
    /// <summary>Fields that give an answer away. On a question, <c>toeicTraps</c> points at the tempting wrong option.</summary>
    public static readonly IReadOnlySet<string> AnswerFields = new HashSet<string>(StringComparer.Ordinal)
        { "answer", "acceptedAnswers", "correction", "explanation", "grammaticalOptions", "correctReason", "toeicTraps" };

    private static readonly IReadOnlySet<string> KnowledgeKeep = new HashSet<string>(StringComparer.Ordinal) { "toeicTraps" };

    public static readonly JsonSerializerOptions PublicJson = new() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    public static GrammarContentBundle Build(IReadOnlyCollection<GrammarDocument> documents)
    {
        var byKey = documents.ToDictionary(d => (d.Kind, d.Key));
        var questions = new Dictionary<string, GrammarQuestionInfo>(StringComparer.Ordinal);
        var unitTitles = new Dictionary<string, string>(StringComparer.Ordinal);
        var traps = new Dictionary<string, string>(StringComparer.Ordinal);
        var topics = new SortedDictionary<string, GrammarTopicContent>(StringComparer.Ordinal);

        foreach (var exercisesDoc in documents.Where(d => d.Kind == GrammarDocumentKind.Exercises).OrderBy(d => d.Key))
        {
            var topic = exercisesDoc.Key;
            if (!byKey.TryGetValue((GrammarDocumentKind.Knowledge, topic), out var knowledgeDoc)
                || !byKey.TryGetValue((GrammarDocumentKind.Vocab, topic), out var vocabDoc))
                throw GrammarErrors.InvalidImportFile($"topic {topic}", "knowledge, vocab and exercises are required.");
            var knowledge = Parse(knowledgeDoc);
            var vocab = Parse(vocabDoc);
            var exercises = Parse(exercisesDoc);
            IndexKnowledge(knowledge["units"] as JsonArray, unitTitles, traps);

            var blocks = new Dictionary<string, GrammarBlock>(StringComparer.Ordinal);
            if (exercises["quickChecks"] is JsonObject quickChecks)
                foreach (var (code, items) in quickChecks)
                {
                    var key = GrammarBlocks.QuickCheckPrefix + code[(code.LastIndexOf('.') + 1)..];
                    blocks[key] = new GrammarBlock(key, QuestionsOf(items as JsonArray, key, topic, null), null);
                }
            var mini = exercises["miniTest"] as JsonObject;
            blocks[GrammarBlocks.MiniTest] = new GrammarBlock(GrammarBlocks.MiniTest,
                QuestionsOf(mini?["questions"] as JsonArray, GrammarBlocks.MiniTest, topic, null), IntOf(mini?["passMark"]));
            foreach (var question in blocks.Values.SelectMany(b => b.Questions)) Register(questions, question);

            var title = knowledge["title"]?.GetValue<string>() ?? string.Empty;
            var version = Hash(knowledgeDoc.ContentHash + vocabDoc.ContentHash + exercisesDoc.ContentHash)[..20];
            var payload = new JsonObject
            {
                ["topic"] = topic,
                ["stage"] = exercises["stage"]?.DeepClone(),
                ["title"] = title,
                ["version"] = version,
                ["knowledge"] = Strip(knowledge, KnowledgeKeep),
                ["vocab"] = Strip(vocab, null),
                ["exercises"] = Strip(exercises, null)
            };
            var words = (vocab["entries"] as JsonArray ?? []).Select(e => e?["word"]?.GetValue<string>())
                .Where(w => !string.IsNullOrEmpty(w)).Select(w => w!).ToHashSet(StringComparer.Ordinal);
            topics[topic] = new GrammarTopicContent
            {
                Topic = topic,
                Stage = exercises["stage"]?.GetValue<string>() ?? string.Empty,
                Title = title,
                Version = version,
                Blocks = blocks,
                PublicJson = JsonSerializer.SerializeToUtf8Bytes(payload, PublicJson),
                VocabWords = words
            };
        }

        var stages = new SortedDictionary<string, GrammarStageContent>(StringComparer.Ordinal);
        foreach (var doc in documents.Where(d => d.Kind == GrammarDocumentKind.StageTest).OrderBy(d => d.Key))
        {
            var test = Parse(doc);
            var stage = doc.Key;
            var block = new GrammarBlock(GrammarBlocks.Stage,
                QuestionsOf(test["questions"] as JsonArray, GrammarBlocks.Stage, null, stage), IntOf(test["passMark"]));
            foreach (var question in block.Questions) Register(questions, question);
            var stageTopics = topics.Values.Where(t => t.Stage == stage).Select(t => t.Topic).OrderBy(x => x).ToList();
            var version = doc.ContentHash[..20];
            var publicTest = (JsonObject)Strip(test, null)!;
            publicTest["stage"] = stage;
            publicTest["version"] = version;
            publicTest["topics"] = new JsonArray(stageTopics.Select(t => (JsonNode?)t).ToArray());
            stages[stage] = new GrammarStageContent
            {
                Stage = stage,
                Title = test["title"]?.GetValue<string>() ?? string.Empty,
                Topics = stageTopics,
                Version = version,
                Block = block,
                PublicTest = publicTest
            };
        }

        return new GrammarContentBundle
        {
            Topics = topics, Stages = stages, Questions = questions, UnitTitles = unitTitles, TrapDescriptions = traps
        };
    }

    /// <summary>Deep copy without answer fields (except <paramref name="keep"/>), at every depth.</summary>
    public static JsonNode? Strip(JsonNode? node, IReadOnlySet<string>? keep) => node switch
    {
        JsonObject obj => new JsonObject(obj
            .Where(p => !AnswerFields.Contains(p.Key) || keep?.Contains(p.Key) == true)
            .Select(p => KeyValuePair.Create(p.Key, Strip(p.Value, keep)))),
        JsonArray arr => new JsonArray(arr.Select(x => Strip(x, keep)).ToArray()),
        null => null,
        _ => node.DeepClone()
    };

    /// <summary>The full answer of a question (reveal): answer, explanation and the kind-specific answer fields.</summary>
    public static JsonObject AnswerOf(GrammarQuestionInfo question)
    {
        var q = question.Question;
        var result = new JsonObject
        {
            ["questionId"] = question.Id,
            ["answer"] = q["answer"]?.DeepClone(),
            ["explanation"] = q["explanation"]?.DeepClone() ?? new JsonObject()
        };
        foreach (var key in new[] { "correction", "acceptedAnswers", "correctReason", "grammaticalOptions", "toeicTraps" })
            if (q.ContainsKey(key)) result[key] = q[key]?.DeepClone();
        return result;
    }

    public static string Hash(string text) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(text)));

    private static JsonObject Parse(GrammarDocument doc) =>
        JsonNode.Parse(doc.Content) as JsonObject
        ?? throw GrammarErrors.InvalidImportFile($"{doc.Kind} {doc.Key}", "document is not a JSON object.");

    private static int? IntOf(JsonNode? node) =>
        node is JsonValue v && v.TryGetValue<int>(out var n) ? n : null;

    private static List<GrammarQuestionInfo> QuestionsOf(JsonArray? items, string block, string? topic, string? stage)
    {
        var result = new List<GrammarQuestionInfo>();
        foreach (var item in (items ?? []).OfType<JsonObject>())
        {
            if (item["kind"]?.GetValue<string>() == "cloze6")
            {
                var blanks = (item["subQuestions"] as JsonArray ?? []).OfType<JsonObject>()
                    .OrderBy(s => IntOf(s["position"]) ?? 0);
                result.AddRange(blanks.Select(s => new GrammarQuestionInfo(Id(s), block, topic, stage, s, item)));
            }
            else result.Add(new GrammarQuestionInfo(Id(item), block, topic, stage, item, null));
        }
        return result;
    }

    private static string Id(JsonObject q) =>
        q["id"]?.GetValue<string>() is { Length: > 0 } id ? id
            : throw GrammarErrors.InvalidImportFile("exercises", "a question has no id.");

    private static void Register(Dictionary<string, GrammarQuestionInfo> index, GrammarQuestionInfo question)
    {
        if (!index.TryAdd(question.Id, question))
            throw GrammarErrors.InvalidImportFile("exercises", $"duplicate question id '{question.Id}'.");
    }

    private static void IndexKnowledge(JsonArray? units, Dictionary<string, string> titles, Dictionary<string, string> traps)
    {
        foreach (var unit in (units ?? []).OfType<JsonObject>())
        {
            if (unit["code"]?.GetValue<string>() is { Length: > 0 } code)
            {
                titles[code] = unit["title"]?.GetValue<string>() ?? string.Empty;
                var i = 0;
                foreach (var trap in (unit["toeicTraps"] as JsonArray ?? []).OfType<JsonObject>())
                    traps[$"{code}#{++i}"] = trap["trap"]?.GetValue<string>() ?? string.Empty;
            }
            IndexKnowledge(unit["subItems"] as JsonArray, titles, traps);
        }
    }
}
