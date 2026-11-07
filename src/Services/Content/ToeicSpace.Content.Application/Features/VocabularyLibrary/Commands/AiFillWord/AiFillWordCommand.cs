using ToeicSpace.Content.Application.DTOs.VocabularyLibrary;

namespace ToeicSpace.Content.Application.Features.VocabularyLibrary.Commands.AiFillWord;

/// <summary>Fills the blank fields of a custom word through the enrichment pipeline (AI included); never overwrites.</summary>
public sealed record AiFillWordCommand(Guid SavedWordId, Guid LearnerId, int TzOffset = 0) : IRequest<AiFillResultDto>;
