namespace ToeicSpace.Content.Domain.Entities;

/// <summary>
/// One row per learner: last study position (resume), page preferences (dark mode, roadmap column width/collapsed)
/// and the known vocabulary words per topic.
/// </summary>
public sealed class GrammarLearnerState
{
    public const int MaxWordsPerTopic = 200;

    public Guid LearnerId { get; set; }
    public string? PositionTopic { get; set; }
    public string? PositionPart { get; set; }
    public string PositionAnchor { get; set; } = string.Empty;
    public bool? DarkMode { get; set; }
    public int? RoadmapWidth { get; set; }
    public bool RoadmapCollapsed { get; set; }
    /// <summary>Known words per topic ("04" → ["revenue", …]), alphabetical.</summary>
    public Dictionary<string, List<string>> KnownWords { get; set; } = [];
    public DateTime UpdatedAt { get; set; }
    public int Version { get; set; }

    /// <summary>Marks or unmarks a known word; idempotent. Returns whether anything changed.</summary>
    public bool SetKnownWord(string topic, string word, bool known)
    {
        var words = KnownWords.TryGetValue(topic, out var list) ? list : [];
        var has = words.Contains(word, StringComparer.Ordinal);
        if (known == has) return false;
        var next = known ? [.. words, word] : words.Where(x => x != word).ToList();
        next.Sort(StringComparer.Ordinal);
        var map = new Dictionary<string, List<string>>(KnownWords);
        if (next.Count == 0) map.Remove(topic);
        else map[topic] = next;
        KnownWords = map;
        return true;
    }

    public void Touch(DateTime now)
    {
        UpdatedAt = now;
        Version++;
    }
}
