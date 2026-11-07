using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace ToeicSpace.Content.Application.Models.Grammar;

/// <summary>A scored question (a cloze6 blank is its own question; <see cref="Parent"/> is the passage).</summary>
public sealed class GrammarQuestionInfo(string id, string block, string? topic, string? stage, JsonObject question,
    JsonObject? parent)
{
    private static readonly Regex TopicOfUnit = new(@"^CĐ(\d{2})", RegexOptions.Compiled);

    public string Id { get; } = id;
    public string Block { get; } = block;
    /// <summary>Topic containing the question; null for a stage-test question.</summary>
    public string? Topic { get; } = topic;
    /// <summary>Stage of a stage-test question; null for a topic question.</summary>
    public string? Stage { get; } = stage;
    public JsonObject Question { get; } = question;
    public JsonObject? Parent { get; } = parent;
    public bool IsClozeBlank => Parent is not null;

    public string UnitCode =>
        FirstString(Question["unitCodes"]) ?? FirstString(Parent?["unitCodes"]) ?? string.Empty;

    public string TrapCode => FirstString(Question["toeicTraps"]) ?? string.Empty;

    /// <summary>Topic of the main unit (a review question of topic 12 may belong to topic 05).</summary>
    public string MainTopic => TopicOfUnit.Match(UnitCode) is { Success: true } m ? m.Groups[1].Value : Topic ?? string.Empty;

    public string Signal => Question["explanation"]?["signal"]?.GetValue<string>() ?? string.Empty;
    public string Rule => Question["explanation"]?["rule"]?.GetValue<string>() ?? string.Empty;

    private static string? FirstString(JsonNode? node) =>
        node is JsonArray { Count: > 0 } list && list[0] is JsonValue v && v.TryGetValue<string>(out var s) ? s : null;
}

/// <summary>A gradable block: <c>qc-u</c> (quick check of unit u), <c>mt</c> (mini test) or <c>stage</c>.</summary>
public sealed class GrammarBlock(string key, IReadOnlyList<GrammarQuestionInfo> questions, int? passMark)
{
    public string Key { get; } = key;
    public IReadOnlyList<GrammarQuestionInfo> Questions { get; } = questions;
    /// <summary>Correct answers needed to pass (mini test, stage test); null for quick checks.</summary>
    public int? PassMark { get; } = passMark;
    public int Total => Questions.Count;
    public IReadOnlySet<string> Ids { get; } = questions.Select(x => x.Id).ToHashSet();
}

public sealed class GrammarTopicContent
{
    public required string Topic { get; init; }
    public required string Stage { get; init; }
    public required string Title { get; init; }
    public required string Version { get; init; }
    public required IReadOnlyDictionary<string, GrammarBlock> Blocks { get; init; }
    /// <summary>Public payload (answers stripped), serialised once.</summary>
    public required byte[] PublicJson { get; init; }
    public required IReadOnlySet<string> VocabWords { get; init; }
    public GrammarBlock MiniTest => Blocks[GrammarBlocks.MiniTest];
}

public sealed class GrammarStageContent
{
    public required string Stage { get; init; }
    public required string Title { get; init; }
    public required IReadOnlyList<string> Topics { get; init; }
    public required string Version { get; init; }
    public required GrammarBlock Block { get; init; }
    /// <summary>Public test (answers stripped) without the viewer's status.</summary>
    public required JsonObject PublicTest { get; init; }
}

/// <summary>Everything the grammar endpoints need from the content, built once per import.</summary>
public sealed class GrammarContentBundle
{
    public required IReadOnlyDictionary<string, GrammarTopicContent> Topics { get; init; }
    public required IReadOnlyDictionary<string, GrammarStageContent> Stages { get; init; }
    public required IReadOnlyDictionary<string, GrammarQuestionInfo> Questions { get; init; }
    /// <summary>Unit and sub-unit titles by code ("CĐ04.1" → "Nhận diện danh từ").</summary>
    public required IReadOnlyDictionary<string, string> UnitTitles { get; init; }
    /// <summary>Trap names by trap code ("CĐ04.1#2").</summary>
    public required IReadOnlyDictionary<string, string> TrapDescriptions { get; init; }

    public GrammarBlock? BlockOf(string? topic, string? stage, string block)
    {
        if (stage is not null)
            return block == GrammarBlocks.Stage && Stages.TryGetValue(stage, out var s) ? s.Block : null;
        return topic is not null && Topics.TryGetValue(topic, out var t) && t.Blocks.TryGetValue(block, out var b) ? b : null;
    }
}

public static class GrammarBlocks
{
    public const string MiniTest = "mt";
    public const string Stage = "stage";
    public const string QuickCheckPrefix = "qc-";
    public static readonly Regex TopicBlock = new(@"^(qc-\d{1,2}|mt)$", RegexOptions.Compiled);
}
