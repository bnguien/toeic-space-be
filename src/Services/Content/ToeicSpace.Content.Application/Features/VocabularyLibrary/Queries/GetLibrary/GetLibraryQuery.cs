using ToeicSpace.Content.Application.DTOs.VocabularyLibrary;

namespace ToeicSpace.Content.Application.Features.VocabularyLibrary.Queries.GetLibrary;

public sealed record GetLibraryQuery(Guid LearnerId, int TzOffset = 0) : IRequest<LibraryDto>;
