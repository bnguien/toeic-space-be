using ToeicSpace.Assessment.Application.Dtos;
using ToeicSpace.BuildingBlocks.Pagination;

namespace ToeicSpace.Assessment.Application.Tests.Queries.GetTests;

/// <param name="Status">Only applied for content managers; learners always get published tests.</param>
public sealed record GetTestsQuery(
    int Page = 1,
    int PageSize = 20,
    string? Category = null,
    int? Year = null,
    ContentStatus? Status = null,
    string? Search = null) : IRequest<PaginatedResult<TestDto>>;
