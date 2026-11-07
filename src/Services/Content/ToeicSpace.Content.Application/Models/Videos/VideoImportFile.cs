namespace ToeicSpace.Content.Application.Models.Videos;

/// <summary>An uploaded data file (<c>.json</c> or gzip-compressed <c>.json.gz</c>).</summary>
public sealed record VideoImportFile(string FileName, byte[] Content);
