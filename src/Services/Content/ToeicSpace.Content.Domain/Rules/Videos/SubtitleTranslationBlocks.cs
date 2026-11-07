namespace ToeicSpace.Content.Domain.Rules.Videos;

/// <summary>
/// Machine translation of subtitles in blocks: 20 lines joined with <c>" ||| "</c> (the translator swallows line
/// breaks but keeps this marker). A block that does not split back into the same number of lines becomes empty
/// lines — never shifted: a misaligned subtitle is worse than none.
/// </summary>
public static class SubtitleTranslationBlocks
{
    public const int LinesPerBlock = 20;
    public const string Separator = " ||| ";

    public static IReadOnlyList<string> Build(IReadOnlyList<string> lines)
    {
        var blocks = new List<string>();
        for (var i = 0; i < lines.Count; i += LinesPerBlock)
            blocks.Add(string.Join(Separator, lines.Skip(i).Take(LinesPerBlock)));
        return blocks;
    }

    /// <summary>One output line per source line; <paramref name="translated"/>[j] may be null (that block failed).</summary>
    public static IReadOnlyList<string> Split(IReadOnlyList<string> blocks, IReadOnlyList<string?> translated)
    {
        var output = new List<string>();
        for (var j = 0; j < blocks.Count; j++)
        {
            var sourceCount = blocks[j].Split(Separator).Length;
            var text = j < translated.Count ? translated[j] ?? string.Empty : string.Empty;
            var parts = text.Split(Separator.Trim()).Select(p => p.Trim()).ToArray();
            output.AddRange(parts.Length == sourceCount ? parts : Enumerable.Repeat(string.Empty, sourceCount));
        }
        return output;
    }
}
