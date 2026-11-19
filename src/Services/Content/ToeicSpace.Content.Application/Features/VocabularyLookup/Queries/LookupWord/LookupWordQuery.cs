using System.Text.Json.Serialization;
using ToeicSpace.Content.Application.DTOs.VocabularyLookup;

namespace ToeicSpace.Content.Application.Features.VocabularyLookup.Queries.LookupWord;

/// <summary>Enriches one word or 2–5 word phrase; <see cref="AllowAi"/> enables the AI tier (slower).</summary>
public sealed record LookupWordQuery(string Text, string? Context = null, bool AllowAi = false,
    [property: JsonIgnore] Guid LearnerId = default) : IRequest<WordLookupDto>
{
    public const int MaxTextLength = 200;
    public const int MaxContextLength = 500;
}
