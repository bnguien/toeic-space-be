using ToeicSpace.Content.Domain.Enums;

namespace ToeicSpace.Content.Application.Models.Enrichment;

/// <summary>
/// One word or phrase to enrich. <paramref name="Known"/> holds fields the caller already has (they are never
/// replaced); <paramref name="PartOfSpeechHint"/> picks the matching official row.
/// </summary>
public sealed record EnrichRequest(string Text, string? Context = null, PartOfSpeech? PartOfSpeechHint = null,
    WordGloss? Known = null);
