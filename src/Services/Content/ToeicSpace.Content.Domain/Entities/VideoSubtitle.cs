namespace ToeicSpace.Content.Domain.Entities;

/// <summary>
/// Subtitle document of one YouTube video (table <c>VideoSubtitles</c>), catalog or pasted link: title/duration
/// snapshot, original language, the language list and every stored track. <see cref="Languages"/> and
/// <see cref="Tracks"/> are JSON documents (shape in docs/video.md) read and written by the subtitle store.
/// </summary>
public sealed class VideoSubtitle
{
    public const int LanguageMaxLength = 16;

    public Guid Id { get; set; } = Guid.NewGuid();
    public string VideoId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    /// <summary>Seconds (YouTube metadata).</summary>
    public int Duration { get; set; }
    /// <summary>Spoken language mapped to a code present in the language list (e.g. <c>en-orig</c>).</summary>
    public string OriginalLanguage { get; set; } = string.Empty;
    /// <summary><c>{"manual":[{code,name}],"auto":[{code,name}]}</c>; null = never listed.</summary>
    public string? Languages { get; set; }
    public DateTime? LanguagesFetchedAt { get; set; }
    /// <summary><c>{"code":{"source":"imported|youtube|translated","cues":[[start,duration,text,offsets?]]}}</c>.</summary>
    public string Tracks { get; set; } = "{}";
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
