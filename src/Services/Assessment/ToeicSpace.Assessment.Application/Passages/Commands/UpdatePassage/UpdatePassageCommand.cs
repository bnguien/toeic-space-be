using ToeicSpace.Assessment.Application.Dtos;
using ToeicSpace.Assessment.Application.Passages.Common;

namespace ToeicSpace.Assessment.Application.Passages.Commands.UpdatePassage;

public sealed record UpdatePassageCommand(
    Guid Id,
    Guid? TestId,
    ToeicPart Part,
    string? PassageType,
    string? Title,
    string? Content,
    string? AudioUrl,
    string? ImageUrl,
    string? Transcript,
    int OrderIndex = 0) : IRequest<PassageDto>, IPassageContent;
