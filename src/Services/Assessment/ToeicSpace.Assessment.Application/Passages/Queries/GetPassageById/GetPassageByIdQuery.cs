using ToeicSpace.Assessment.Application.Dtos;

namespace ToeicSpace.Assessment.Application.Passages.Queries.GetPassageById;

public sealed record GetPassageByIdQuery(Guid Id) : IRequest<PassageDetailDto>;
