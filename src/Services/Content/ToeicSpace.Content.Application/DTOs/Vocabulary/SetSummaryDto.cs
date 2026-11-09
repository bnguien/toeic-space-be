namespace ToeicSpace.Content.Application.DTOs.Vocabulary;

/// <summary><paramref name="PreviewWords"/> are the first 3 headwords, <paramref name="LastWord"/> the last one in lesson order.</summary>
public sealed record SetSummaryDto(Guid Id, Guid StageId, string Slug, string Title, int Order, int WordCount,
    IReadOnlyList<string> PreviewWords, string LastWord);
