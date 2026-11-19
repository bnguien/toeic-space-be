using System.Text.Json.Serialization;
using ToeicSpace.Content.Application.DTOs.Dictionary;

namespace ToeicSpace.Content.Application.Features.Dictionary.Queries.LookupSelection;

/// <summary>
/// Looks up the text a learner selected anywhere on the site: a word or a 2–5 word phrase goes through the
/// vocabulary enrichment tiers, longer text is translated. <see cref="AllowAi"/> is honoured only for signed-in
/// callers (<see cref="LearnerId"/> set); guests always get the fast tiers.
/// </summary>
public sealed record LookupSelectionQuery(string Text, string? Context = null, bool AllowAi = false,
    [property: JsonIgnore] Guid LearnerId = default) : IRequest<SelectionLookupDto>
{
    /// <summary>Same cap as the client: a selection is cut to this many characters before it is sent.</summary>
    public const int MaxTextLength = 600;
    public const int MaxContextLength = 500;
}
