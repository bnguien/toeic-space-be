namespace ToeicSpace.Content.Application.DTOs.Videos;

/// <summary>Other learners already share this video: total sharers and the first one's name (shown on the card).</summary>
public sealed record DuplicateShareDto(int Count, string FirstBy);
