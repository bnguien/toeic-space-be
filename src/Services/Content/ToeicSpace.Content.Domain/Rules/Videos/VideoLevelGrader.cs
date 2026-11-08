using ToeicSpace.Content.Domain.Enums;
using ToeicSpace.Content.Domain.ValueObjects;

namespace ToeicSpace.Content.Domain.Rules.Videos;

/// <summary>Measurements of one English subtitle track (Studychill <c>video_level.measure_subtitles</c>).</summary>
public sealed record SubtitleMeasurements(int Words, double SpeechSeconds, double WordsPerMinute, double SentenceLength,
    double LexicalScore, int ContentWords, IReadOnlyDictionary<string, double> Mix);

/// <summary>Difficulty (0–100), CEFR band and listening path group of a catalog video.</summary>
public sealed record VideoLevel(int Difficulty, string Cefr, VideoTrack Track);

/// <summary>
/// Catalog video level (Studychill <c>video_level.py</c>): difficulty = 75% subtitle measurements (speech rate,
/// sentence length when punctuated, vocabulary difficulty, duration) + 25% channel prior; videos with little speech
/// lean on the prior. Thresholds calibrated on 77 real videos (2026-10-17).
/// </summary>
public static class VideoLevelGrader
{
    public static readonly IReadOnlyDictionary<string, int> ChannelScore = new Dictionary<string, int>
    {
        ["A1"] = 8, ["A2"] = 22, ["B1"] = 42, ["B2"] = 58, ["C1"] = 74, ["C2"] = 88
    };
    private static readonly (int Upper, string Cefr)[] CefrBands =
        [(18, "A1"), (34, "A2"), (47, "B1"), (61, "B2"), (79, "C1"), (101, "C2")];
    public const double PriorWeight = 0.25;
    public const int MinWords = 120;
    public const double SlowWordsPerMinute = 90;
    private static readonly IReadOnlyDictionary<string, double> WordWeights = new Dictionary<string, double>
    {
        ["basic"] = 0.0, ["b1"] = 0.35, ["b2"] = 0.7, ["c1"] = 1.0, ["unknownMid"] = 0.4, ["unknownLong"] = 0.8
    };
    private static readonly string[] WeightKeys = ["basic", "b1", "b2", "c1", "unknownMid", "unknownLong"];
    private const string Punct = ".,!?;:\"“”‘’()[]{}<>…—–-*_/|~`@#$%^&+=♪♫";
    private static readonly (string Suffix, string Replacement)[] StemRules =
    [
        ("ies", "y"), ("ied", "y"), ("ing", ""), ("ing", "e"), ("ed", ""), ("ed", "e"), ("es", ""), ("s", ""),
        ("er", ""), ("est", ""), ("ly", "")
    ];

    private static readonly HashSet<string> FunctionWords =
    [
        "a", "an", "the", "and", "or", "but", "so", "if", "then", "than", "that", "this", "these", "those", "there",
        "here", "what", "which", "who", "whom", "whose", "when", "where", "why", "how", "i", "me", "my", "mine", "you",
        "your", "yours", "he", "him", "his", "she", "her", "hers", "it", "its", "we", "us", "our", "ours", "they",
        "them", "their", "theirs", "myself", "yourself", "himself", "herself", "itself", "ourselves", "themselves",
        "is", "am", "are", "was", "were", "be", "been", "being", "do", "does", "did", "done", "doing", "have", "has",
        "had", "having", "will", "would", "shall", "should", "can", "could", "may", "might", "must", "not", "no",
        "nor", "yes", "to", "of", "in", "on", "at", "by", "for", "with", "from", "up", "down", "out", "off", "over",
        "under", "into", "onto", "about", "as", "just", "also", "too", "very", "all", "any", "some", "each", "every",
        "both", "few", "more", "most", "other", "such", "only", "own", "same", "again", "once", "oh", "ok", "okay",
        "yeah", "uh", "um", "hmm", "ah", "well", "like", "gonna", "wanna", "gotta", "let", "lets", "let's", "i'm",
        "you're", "he's", "she's", "it's", "we're", "they're", "i've", "you've", "we've", "they've", "i'll",
        "you'll", "he'll", "she'll", "we'll", "they'll", "i'd", "you'd", "he'd", "she'd", "we'd", "they'd", "don't",
        "doesn't", "didn't", "isn't", "aren't", "wasn't", "weren't", "won't", "wouldn't", "can't", "couldn't",
        "shouldn't", "haven't", "hasn't", "hadn't", "that's", "there's", "what's", "here's", "who's", "how's",
        "where's"
    ];

    public static VideoTrack TrackOf(string cefr) => cefr switch
    {
        "A1" or "A2" => VideoTrack.Easy,
        "B1" or "B2" => VideoTrack.Medium,
        "C1" or "C2" => VideoTrack.Hard,
        _ => VideoTrack.None
    };

    /// <summary>Pure measurements of a subtitle track.</summary>
    public static SubtitleMeasurements Measure(IReadOnlyList<SubtitleCue> allCues, WordLevelTable table)
    {
        var cues = allCues.Where(c => !IsSoundCue(c.Text)).ToList();
        var speechSeconds = 0.0;
        var tokens = new List<(string Token, bool SentenceStart)>();
        var sentenceEnds = 0;
        for (var i = 0; i < cues.Count; i++)
        {
            var start = cues[i].Start;
            var end = start + cues[i].Duration;
            if (i + 1 < cues.Count && cues[i + 1].Start > start) end = Math.Min(end, cues[i + 1].Start);
            speechSeconds += Math.Max(0, Math.Min(end - start, 12.0));
            char? closing = null;
            foreach (var token in cues[i].Text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries))
            {
                if (closing is null && token[0] is '[' or '(') closing = token[0] == '[' ? ']' : ')';
                if (closing is { } close)
                {
                    if (token.Contains(close)) closing = null;
                    continue;
                }
                var sentenceStart = tokens.Count == 0 || ".?!".Contains(tokens[^1].Token[^1]);
                tokens.Add((token, sentenceStart));
                var trimmed = token.TrimEnd('"', '\'', ')', ']', '”', '’');
                // Python: "" in ".?!" is True — a token made only of closing quotes counts as a sentence end.
                if (trimmed.Length == 0 || ".?!".Contains(trimmed[^1])) sentenceEnds++;
            }
        }

        var words = tokens.Where(t => t.Token.Any(char.IsLetter)).ToList();
        var wordCount = words.Count;
        var hasPunctuation = wordCount > 0 && sentenceEnds >= wordCount / 45.0;
        var store = CefrLookup(words.Select(t => Clean(t.Token)).ToHashSet(), table);
        var counts = WeightKeys.ToDictionary(k => k, _ => 0);
        var weightSum = 0.0;
        var content = 0;
        foreach (var (token, sentenceStart) in words)
        {
            var w = Clean(token);
            if (w.Length == 0 || !w.Replace("'", string.Empty).All(char.IsLetter) || w.Replace("'", string.Empty).Length == 0
                || FunctionWords.Contains(w)) continue;
            // Proper noun: capitalised mid-sentence (automatic captions are lowercase except names).
            var stripped = token.Trim(Punct.ToCharArray());
            if (stripped.Length > 0 && char.IsUpper(stripped[0]) && w != "i" && !(sentenceStart && hasPunctuation)) continue;
            var tier = WordTier(w, table.Basic, store);
            counts[tier]++;
            weightSum += WordWeights[tier];
            content++;
        }

        var minutes = speechSeconds / 60.0;
        return new SubtitleMeasurements(
            wordCount,
            Round(speechSeconds, 1),
            minutes >= 0.1 ? Round(wordCount / minutes, 1) : 0.0,
            hasPunctuation ? Round((double)wordCount / sentenceEnds, 1) : 0.0,
            content > 0 ? Round(weightSum / content, 3) : 0.0,
            content,
            content > 0 ? counts.ToDictionary(x => x.Key, x => Round((double)x.Value / content, 3))
                : new Dictionary<string, double>());
    }

    /// <summary>Score 0–100 from the measurements only (before the channel prior).</summary>
    public static double DifficultyScore(SubtitleMeasurements m, int duration)
    {
        var lexical = Ratio(m.LexicalScore, 0.06, 0.30);
        var speed = Ratio(m.WordsPerMinute, 95, 185);
        // Very slow speech (songs, word lists for kids) makes unknown words easy to hear out.
        if (m.WordsPerMinute is > 0 and < SlowWordsPerMinute) lexical *= Math.Max(0.4, m.WordsPerMinute / SlowWordsPerMinute);
        var length = Ratio(duration / 60.0, 2, 18);
        if (m.SentenceLength > 0)
            return 0.45 * lexical + 0.30 * speed + 0.15 * Ratio(m.SentenceLength, 6, 20) + 0.10 * length;
        return 0.55 * lexical + 0.35 * speed + 0.10 * length;
    }

    public static string CefrForScore(double score) => CefrBands.First(b => score < b.Upper).Cefr;

    public static VideoLevel Grade(SubtitleMeasurements m, int duration, string? channelLevel)
    {
        var prior = ChannelScore.TryGetValue((channelLevel ?? string.Empty).ToUpperInvariant(), out var p) ? p : 50;
        var weight = m.Words >= MinWords ? PriorWeight : 0.6;
        var score = (int)Math.Round((1 - weight) * DifficultyScore(m, duration) + weight * prior, MidpointRounding.ToEven);
        var cefr = CefrForScore(score);
        return new VideoLevel(score, cefr, TrackOf(cefr));
    }

    /// <summary>No subtitles yet: band = channel level; score = prior nudged by duration so short videos come first.</summary>
    public static VideoLevel Estimate(string? channelLevel, int duration)
    {
        var level = (channelLevel ?? string.Empty).ToUpperInvariant();
        if (!ChannelScore.ContainsKey(level)) level = "B1";
        var offset = Math.Clamp((duration / 60.0 - 5) * 0.8, -4.0, 6.0);
        return new VideoLevel((int)Math.Round(ChannelScore[level] + offset, MidpointRounding.ToEven), level, TrackOf(level));
    }

    public static bool IsSoundCue(string text)
    {
        var t = text.Trim();
        return t.Length == 0 || (t[0] is '[' or '(' && t[^1] is ']' or ')') || t.All(c => c is '♪' or '♫' or ' ');
    }

    public static IReadOnlyList<string> Stems(string word)
    {
        var stems = new List<string> { word };
        foreach (var (suffix, replacement) in StemRules)
        {
            if (word.Length <= suffix.Length + 2 || !word.EndsWith(suffix, StringComparison.Ordinal)) continue;
            var stem = word[..^suffix.Length] + replacement;
            stems.Add(stem);
            if (stem.Length > 2 && stem[^1] == stem[^2]) stems.Add(stem[..^1]);
        }
        return stems;
    }

    private static Dictionary<string, string> CefrLookup(IEnumerable<string> words, WordLevelTable table)
    {
        var candidates = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var word in words)
            foreach (var stem in Stems(word))
                candidates.Add(stem);
        return WordLevelRules.Levels(candidates, table);
    }

    private static string WordTier(string word, IReadOnlySet<string> basic, IReadOnlyDictionary<string, string> store)
    {
        var stems = Stems(word);
        if (stems.Any(basic.Contains)) return "basic";
        foreach (var stem in stems)
            if (store.TryGetValue(stem, out var cefr) && cefr.Length > 0)
                return cefr is "C1" or "C2" ? "c1" : cefr == "B2" ? "b2" : "b1";
        // A root of ≤ 4 letters is nearly always a common word (going → go, makes → make).
        if (stems.Any(s => s.Length <= 4)) return "basic";
        return word.Length >= 8 ? "unknownLong" : "unknownMid";
    }

    private static string Clean(string token) => token.Trim(Punct.ToCharArray()).Replace('’', '\'').ToLowerInvariant();

    private static double Ratio(double x, double low, double high) => Math.Clamp((x - low) / (high - low), 0, 1) * 100;

    private static double Round(double value, int digits) => Math.Round(value, digits, MidpointRounding.ToEven);
}
