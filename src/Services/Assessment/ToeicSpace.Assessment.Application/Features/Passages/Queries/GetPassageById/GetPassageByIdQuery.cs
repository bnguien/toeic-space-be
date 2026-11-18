using ToeicSpace.Assessment.Application.DTOs.Passages;


namespace ToeicSpace.Assessment.Application.Features.Passages.Queries.GetPassageById;

public sealed record GetPassageByIdQuery(Guid Id) : IRequest<PassageDetailDto>;
