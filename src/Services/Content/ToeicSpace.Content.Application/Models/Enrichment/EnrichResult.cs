namespace ToeicSpace.Content.Application.Models.Enrichment;

/// <summary>
/// Pipeline outcome: merged gloss (with provenance), whether it is complete, whether AI ran, the official word when
/// the catalog matched, and the fields the pipeline added on top of the request's known fields.
/// </summary>
public sealed record EnrichResult(WordGloss Gloss, bool Complete, bool AiUsed, Guid? OfficialWordId,
    IReadOnlyList<GlossField> Filled);
