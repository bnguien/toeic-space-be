namespace ToeicSpace.Content.Application.DTOs.News;

/// <summary>One uploaded file of the news import: an articles document (.json / .json.gz) or a cover (.webp).</summary>
public sealed record NewsImportFile(string FileName, string? ContentType, long SizeBytes, byte[] Content);

/// <summary>
/// Counts of one import call. <c>MissingCovers</c> = slugs whose cover file is not in the cover store yet (upload it
/// in another call; the article keeps the reference).
/// </summary>
public sealed record NewsImportResultDto(int ArticlesCreated, int ArticlesUpdated, int ArticlesUnchanged,
    int CoversStored, int CoversUnchanged, IReadOnlyList<string> MissingCovers);
