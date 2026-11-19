using ToeicSpace.Content.Application.Interfaces.Services.Enrichment;
using ToeicSpace.Content.Domain.Rules.Videos;
using ToeicSpace.Content.Domain.ValueObjects;

namespace ToeicSpace.Content.Application.Services.Video;

/// <summary>
/// Vietnamese subtitles by machine translation when YouTube has none: blocks of 20 lines joined with " ||| "
/// (10× faster than line by line and keeps context), ≤ 4 blocks in flight. Timings are copied from the source track.
/// </summary>
public sealed class SubtitleTranslator(ITranslationProvider translation)
{
    public const int Parallelism = 4;

    /// <summary>Translated cues (same timing, no word offsets), or empty when nothing could be translated.</summary>
    public async Task<IReadOnlyList<SubtitleCue>> TranslateAsync(IReadOnlyList<SubtitleCue> source, CancellationToken ct)
    {
        if (source.Count == 0) return [];
        var blocks = SubtitleTranslationBlocks.Build(source.Select(c => c.Text ?? string.Empty).ToList());
        var translated = new string?[blocks.Count];
        await Parallel.ForEachAsync(Enumerable.Range(0, blocks.Count),
            new ParallelOptions { MaxDegreeOfParallelism = Parallelism, CancellationToken = ct },
            async (j, token) => translated[j] = (await translation.TranslateAsync(blocks[j], token))?.Text);
        var lines = SubtitleTranslationBlocks.Split(blocks, translated);
        if (!lines.Any(l => l.Length > 0)) return [];
        return source.Select((cue, i) => new SubtitleCue(cue.Start, cue.Duration, i < lines.Count ? lines[i] : string.Empty))
            .ToList();
    }
}
