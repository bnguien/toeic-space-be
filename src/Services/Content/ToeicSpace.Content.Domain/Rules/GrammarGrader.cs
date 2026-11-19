using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace ToeicSpace.Content.Domain.Rules;

/// <summary>
/// Server-side grading of grammar answers — a 1:1 port of Studychill's grading (grammarGrading.js / grading.py) on the
/// English content keys. Keeps the JavaScript semantics on purpose: JS <c>\s</c> whitespace class, lower-casing, curly
/// quotes → straight, collapsed spaces, trailing <c>. ? !</c> dropped, accepted-answer lists, transform written as the
/// whole sentence or the part after the hint, order compared case-insensitively without commas. Any JSON value is
/// accepted and a wrong shape is simply wrong — grading never throws.
/// Answer values: mcq5 / pair990 / cloze6 blank "A".."D"; wordform / transform text; error {part, correction};
/// judge {verdict: "correct"|"incorrect", reason: "A".."C"}; order sentence or piece indexes [2, 0, 1];
/// parse labels ["S", "V", …]; match {"1": "C", …} or letters in left order.
/// </summary>
public static class GrammarGrader
{
    private const string Ws = "\\t\\n\\v\\f\\r \\u00a0\\u1680\\u2000-\\u200a\\u2028\\u2029\\u202f\\u205f\\u3000\\ufeff";
    private static readonly Regex SpaceBeforePunct = new($"[{Ws}]+([,.;:?!])", RegexOptions.Compiled);
    private static readonly Regex Spaces = new($"[{Ws}]+", RegexOptions.Compiled);
    private static readonly Regex Trim = new($"^[{Ws}]+|[{Ws}]+$", RegexOptions.Compiled);
    private static readonly Regex EndPunct = new("[.?!]+$", RegexOptions.Compiled);
    private const string Mismatch = "\0";

    /// <summary>`String(x ?? '')` for valid scalar kinds; objects and arrays become a string matching nothing.</summary>
    public static string JsString(JsonNode? value)
    {
        if (value is null) return string.Empty;
        if (value is not JsonValue scalar) return Mismatch;
        var element = scalar.GetValue<JsonElement>();
        return element.ValueKind switch
        {
            JsonValueKind.String => element.GetString() ?? string.Empty,
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            JsonValueKind.Number => element.TryGetInt64(out var n)
                ? n.ToString(CultureInfo.InvariantCulture)
                : element.GetDouble().ToString(CultureInfo.InvariantCulture),
            JsonValueKind.Null or JsonValueKind.Undefined => string.Empty,
            _ => Mismatch
        };
    }

    private static string TrimJs(string s) => Trim.Replace(s, string.Empty);

    /// <summary>Lower-case, curly quotes → straight, collapse spaces, drop the trailing <c>. ? !</c>.</summary>
    public static string Normalize(JsonNode? value) => NormalizeText(JsString(value));

    public static string NormalizeText(string text)
    {
        var v = text.ToLowerInvariant()
            .Replace('’', '\'').Replace('‘', '\'').Replace('“', '"').Replace('”', '"');
        v = SpaceBeforePunct.Replace(v, "$1");
        v = TrimJs(Spaces.Replace(v, " "));
        return TrimJs(EndPunct.Replace(v, string.Empty));
    }

    /// <summary>Like <see cref="NormalizeText"/> but every comma is dropped too (order questions).</summary>
    public static string NormalizeOrder(string text) => NormalizeText(text.Replace(",", " "));

    private static bool Contains(string value, IEnumerable<JsonNode?> items, Func<string, string> normalize)
    {
        var v = normalize(value);
        return v.Length > 0 && items.Any(item => normalize(JsString(item)) == v);
    }

    private static string? Str(JsonNode? node) =>
        node is JsonValue v && v.GetValueKind() == JsonValueKind.String ? v.GetValue<string>() : null;

    private static JsonArray? Arr(JsonNode? node) => node as JsonArray;

    /// <summary>`q.acceptedAnswers || [q.answer]` — an empty array still counts (JS truthiness).</summary>
    private static IEnumerable<JsonNode?> AcceptedOrAnswer(JsonObject q) =>
        Arr(q["acceptedAnswers"]) is { } accepted ? accepted.ToArray() : new[] { q["answer"] };

    private static IEnumerable<JsonNode?> With(JsonNode? first, JsonNode? accepted) =>
        Arr(accepted) is { } list ? list.Prepend(first).ToArray() : new[] { first };

    /// <summary>The sentence of an order answer: the string as sent, or piece indexes (each piece at most once).</summary>
    public static string? OrderSentence(JsonObject q, JsonNode? value)
    {
        if (Str(value) is { } sentence) return sentence;
        var pieces = Arr(q["pieces"])?.Select(p => JsString(p)).ToList() ?? [];
        if (value is not JsonArray indexes || indexes.Count == 0 || indexes.Count > pieces.Count) return null;
        var picked = new List<int>(indexes.Count);
        foreach (var node in indexes)
        {
            if (node is not JsonValue v || v.GetValueKind() != JsonValueKind.Number) return null;
            var element = v.GetValue<JsonElement>();
            if (!element.TryGetInt32(out var i) || element.GetRawText().Contains('.') || i < 0 || i >= pieces.Count)
                return null;
            picked.Add(i);
        }
        if (picked.Distinct().Count() != picked.Count) return null;
        return string.Join(' ', picked.Select(i => pieces[i]));
    }

    private static List<JsonNode?>? MatchLetters(JsonObject q, JsonNode? value)
    {
        var n = (Arr(q["left"]) ?? Arr(q["answer"]))?.Count ?? 0;
        if (value is JsonArray list) return [.. list];
        if (value is JsonObject map)
            return Enumerable.Range(1, n).Select(i => map[i.ToString(CultureInfo.InvariantCulture)]).ToList();
        return null;
    }

    /// <summary>Grades one question (not a cloze6 blank).</summary>
    public static bool GradeQuestion(JsonObject q, JsonNode? value)
    {
        switch (Str(q["kind"]))
        {
            case "mcq5":
            case "pair990":
                return Str(value) is { } letter && letter == Str(q["answer"]);
            case "wordform":
                return Contains(JsString(value), AcceptedOrAnswer(q), NormalizeText);
            case "transform":
            {
                var written = TrimJs(JsString(value));
                if (written.Length == 0 || written == Mismatch) return false;
                var items = AcceptedOrAnswer(q).ToList();
                return Contains(written, items, NormalizeText)
                       || Contains($"{JsString(q["hint"])} {written}", items, NormalizeText);
            }
            case "order":
            {
                var sentence = OrderSentence(q, value);
                return sentence is not null && Contains(sentence, With(q["answer"], q["acceptedAnswers"]), NormalizeOrder);
            }
            case "error":
                if (value is not JsonObject error || Str(error["part"]) is not { } part || part != Str(q["answer"]))
                    return false;
                return Contains(JsString(error["correction"]), With(q["correction"], q["acceptedAnswers"]), NormalizeText);
            case "parse":
            {
                if (value is not JsonArray labels) return false;
                var expected = Arr(q["answer"]) ?? [];
                for (var i = 0; i < expected.Count; i++)
                    if (i >= labels.Count || !JsonNode.DeepEquals(labels[i], expected[i])) return false;
                return true;
            }
            case "match":
            {
                var letters = MatchLetters(q, value);
                if (letters is null) return false;
                var answers = Arr(q["answer"]) ?? [];
                for (var i = 0; i < answers.Count; i++)
                    if (i >= letters.Count || Str(letters[i]) is not { } picked || Str(answers[i]) != $"{i + 1}-{picked}")
                        return false;
                return true;
            }
            case "judge":
                return value is JsonObject judge && Str(judge["verdict"]) is { } verdict && verdict == Str(q["answer"])
                       && Str(judge["reason"]) is { } reason && reason == Str(q["correctReason"]);
            default:
                return false;
        }
    }

    /// <summary>One Part 6 blank, graded by its own id; the value is "A".."D".</summary>
    public static bool GradeClozeBlank(JsonObject blank, JsonNode? value) =>
        Str(value) is { } letter && letter == Str(blank["answer"]);

    public static bool Grade(JsonObject q, JsonNode? value, bool isClozeBlank) =>
        isClozeBlank ? GradeClozeBlank(q, value) : GradeQuestion(q, value);

    /// <summary>A blank answer scores 0 but does not count as answered (no "first answer", no mistake book).</summary>
    public static bool IsBlank(JsonNode? value) => value switch
    {
        null => true,
        JsonValue v when v.GetValueKind() == JsonValueKind.String => TrimJs(v.GetValue<string>()).Length == 0,
        JsonValue v when v.GetValueKind() == JsonValueKind.Null => true,
        JsonArray a => a.Count == 0,
        JsonObject o => o.All(x => IsBlank(x.Value)),
        _ => false
    };

    /// <summary>The canonical answer built from the content (shared test: every content answer must grade right).</summary>
    public static JsonNode? CanonicalAnswer(JsonObject q, bool isClozeBlank)
    {
        if (isClozeBlank) return q["answer"]?.DeepClone();
        return Str(q["kind"]) switch
        {
            "error" => new JsonObject { ["part"] = q["answer"]?.DeepClone(), ["correction"] = q["correction"]?.DeepClone() },
            "match" => new JsonObject((Arr(q["answer"]) ?? []).Select((cap, i) =>
                KeyValuePair.Create((i + 1).ToString(CultureInfo.InvariantCulture),
                    (JsonNode?)JsonValue.Create(JsString(cap).Split('-', 2).ElementAtOrDefault(1) ?? string.Empty)))),
            "judge" => new JsonObject { ["verdict"] = q["answer"]?.DeepClone(), ["reason"] = q["correctReason"]?.DeepClone() },
            _ => q["answer"]?.DeepClone()
        };
    }
}
