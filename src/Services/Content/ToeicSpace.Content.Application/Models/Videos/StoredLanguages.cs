using ToeicSpace.Content.Domain.ValueObjects;

namespace ToeicSpace.Content.Application.Models.Videos;

/// <summary>The stored language list of a video with its title/duration snapshot and original language.</summary>
public sealed record StoredLanguages(IReadOnlyList<VideoLanguage> Manual, IReadOnlyList<VideoLanguage> Auto, string Title,
    int Duration, string OriginalLanguage);
