using System.Text.Json.Serialization;
using ToeicSpace.Content.Application.DTOs.News;

namespace ToeicSpace.Content.Application.Features.News.Commands.UpdateNewsReadingState;

/// <summary>
/// Partial update of the learner's reading; absent (null) properties are kept. <c>ReadingSeconds</c> only grows,
/// <c>KnownWords</c> and <c>Notes</c> ("" clears) replace, <c>Dictation</c> entries merge forward (after
/// <c>ResetDictation</c> when set), <c>Settings</c> replace the learner's reader settings.
/// </summary>
public sealed record UpdateNewsReadingStateCommand(string Slug, int? ReadingSeconds = null,
    IReadOnlyList<string>? KnownWords = null, string? Notes = null,
    IReadOnlyDictionary<string, NewsDictationEntryInput>? Dictation = null, bool ResetDictation = false,
    NewsReaderSettingsDto? Settings = null, [property: JsonIgnore] Guid LearnerId = default)
    : IRequest<NewsReadingStateDto>;
