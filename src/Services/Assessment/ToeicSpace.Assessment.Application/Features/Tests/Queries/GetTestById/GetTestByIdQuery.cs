using ToeicSpace.Assessment.Application.DTOs.Tests;


namespace ToeicSpace.Assessment.Application.Features.Tests.Queries.GetTestById;

public sealed record GetTestByIdQuery(Guid Id) : IRequest<TestDetailDto>;
