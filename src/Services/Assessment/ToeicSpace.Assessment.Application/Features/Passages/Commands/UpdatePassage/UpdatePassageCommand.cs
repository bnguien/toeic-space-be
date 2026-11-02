using ToeicSpace.Assessment.Application.Common.Validation;
using ToeicSpace.Assessment.Application.DTOs.Passages;


namespace ToeicSpace.Assessment.Application.Features.Passages.Commands.UpdatePassage;

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
