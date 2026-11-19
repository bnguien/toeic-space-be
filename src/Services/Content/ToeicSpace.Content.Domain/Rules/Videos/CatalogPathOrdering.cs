namespace ToeicSpace.Content.Domain.Rules.Videos;

/// <summary>
/// Listening path order (Studychill <c>selectors._order_path</c>): difficulty ascending; inside one band of
/// <see cref="Band"/> points topics are interleaved, each topic shortest video first.
/// </summary>
public static class CatalogPathOrdering
{
    public const int Band = 6;
    public const int MaxVideos = 300;

    public static IReadOnlyList<T> Order<T>(IEnumerable<T> rows, Func<T, int> difficulty, Func<T, int> duration,
        Func<T, string> videoId, Func<T, string> topic)
    {
        var sorted = rows.OrderBy(difficulty).ThenBy(duration).ThenBy(videoId, StringComparer.Ordinal);
        var bands = new SortedDictionary<int, List<(string Topic, List<T> Items)>>();
        foreach (var row in sorted)
        {
            var key = (int)Math.Floor(difficulty(row) / (double)Band);
            if (!bands.TryGetValue(key, out var groups)) bands[key] = groups = [];
            var group = groups.FirstOrDefault(g => g.Topic == topic(row));
            if (group.Items is null) groups.Add((topic(row), [row]));
            else group.Items.Add(row);
        }

        var result = new List<T>();
        foreach (var groups in bands.Values)
        {
            var lanes = groups.OrderBy(g => duration(g.Items[0])).Select(g => g.Items).ToList();
            var longest = lanes.Max(l => l.Count);
            for (var i = 0; i < longest; i++)
                foreach (var lane in lanes)
                    if (i < lane.Count) result.Add(lane[i]);
        }
        return result;
    }
}
