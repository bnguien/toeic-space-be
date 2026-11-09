using System.Text.Json.Serialization;
using ToeicSpace.Content.Application.DTOs.Videos;

namespace ToeicSpace.Content.Application.Features.VideoWords.Queries.GlossWords;

/// <summary>Meanings of transcript words at level cache | free | ai (ai only when signed in).</summary>
public sealed record GlossWordsQuery(IReadOnlyList<string?>? Words, string? Level = "free",
    [property: JsonIgnore] bool SignedIn = false) : IRequest<GlossResultDto>;
