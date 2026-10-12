using ToeicSpace.Assessment.Application.Dtos;

namespace ToeicSpace.Assessment.Application.Tests.Queries.GetTestById;

public sealed record GetTestByIdQuery(Guid Id) : IRequest<TestDetailDto>;
