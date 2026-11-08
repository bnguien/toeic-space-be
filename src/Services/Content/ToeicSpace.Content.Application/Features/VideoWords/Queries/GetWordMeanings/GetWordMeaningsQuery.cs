using ToeicSpace.Content.Application.DTOs.Videos;

namespace ToeicSpace.Content.Application.Features.VideoWords.Queries.GetWordMeanings;

/// <summary>Basic meanings (no AI) of up to 20 comma-separated words — guests on the lesson summary.</summary>
public sealed record GetWordMeaningsQuery(string? Words) : IRequest<WordMeaningsDto>
{
    public IReadOnlyList<string> WordList => (Words ?? string.Empty).Split(',').Select(x => x.Trim())
        .Where(x => x.Length > 0).ToList();
}
