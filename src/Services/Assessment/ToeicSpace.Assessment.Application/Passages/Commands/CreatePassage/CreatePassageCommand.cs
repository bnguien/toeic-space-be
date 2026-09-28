using ToeicSpace.Assessment.Application.Dtos;
using ToeicSpace.Assessment.Application.Passages.Common;

namespace ToeicSpace.Assessment.Application.Passages.Commands.CreatePassage;

public sealed record CreatePassageCommand(
    Guid? TestId,
    ToeicPart Part,
    string? PassageType,
    string? Title,
    string? Content,
    string? AudioUrl,
    string? ImageUrl,
    string? Transcript,
    int OrderIndex = 0) : IRequest<PassageDto>, IPassageContent;
