using System.Text.Json.Serialization;
using ToeicSpace.Content.Application.DTOs.News;

namespace ToeicSpace.Content.Application.Features.News.Commands.MergeGuestReadings;

/// <summary>
/// Reading state a guest kept in the browser, merged into the account after sign-in (Studychill learnerSync rules):
/// the reading clock and completion take the larger value, known words and notes keep the newer copy
/// (<c>UpdatedAt</c> of the browser copy against the stored row), dictation progress only moves forward.
/// </summary>
public sealed record MergeGuestReadingsCommand(IReadOnlyList<NewsGuestReadingInput> Items,
    [property: JsonIgnore] Guid LearnerId = default) : IRequest<NewsMergeResultDto>
{
    public const int MaxItems = 300;
}

/// <summary>One article of the guest's browser state.</summary>
public sealed record NewsGuestReadingInput(string Slug, int ReadingSeconds = 0, bool Completed = false,
    IReadOnlyList<string>? KnownWords = null, string? Notes = null,
    IReadOnlyDictionary<string, NewsDictationEntryInput>? Dictation = null, DateTimeOffset? UpdatedAt = null);
