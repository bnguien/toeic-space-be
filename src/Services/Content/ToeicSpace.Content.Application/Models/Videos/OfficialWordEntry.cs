using ToeicSpace.Content.Domain.Enums;

namespace ToeicSpace.Content.Application.Models.Videos;

/// <summary>An official roadmap word used for the word card's family list.</summary>
public sealed record OfficialWordEntry(string Headword, string MeaningVi, PartOfSpeech PartOfSpeech);
