namespace ToeicSpace.Content.Domain.ValueObjects;

/// <summary>A subtitle language of a YouTube video (<c>code</c> as YouTube/yt-dlp names it, e.g. <c>en-orig</c>).</summary>
public sealed record VideoLanguage(string Code, string Name);
