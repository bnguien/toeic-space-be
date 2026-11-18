using ToeicSpace.Assessment.Application.DTOs.PracticeSets;


namespace ToeicSpace.Assessment.Application.Features.PracticeSets.Queries.GetPracticeSetById;

public sealed record GetPracticeSetByIdQuery(Guid Id) : IRequest<PracticeSetDetailDto>;
