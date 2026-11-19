using ToeicSpace.Content.Domain.Enums;

namespace ToeicSpace.Content.Application.Models.Enrichment;

/// <summary>One word for the AI batch prompt with what is already known (hints keep the AI consistent).</summary>
public sealed record AiWordRequest(string Word, PartOfSpeech? PartOfSpeech, string? MeaningVi);
