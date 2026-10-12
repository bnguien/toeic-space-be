using ToeicSpace.Assessment.Application.Dtos;

namespace ToeicSpace.Assessment.Application.Practice.Queries.GetPracticeSetById;

public sealed record GetPracticeSetByIdQuery(Guid Id) : IRequest<PracticeSetDetailDto>;
