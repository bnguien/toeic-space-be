using ToeicSpace.Assessment.Application.Dtos;
using ToeicSpace.BuildingBlocks.Pagination;

namespace ToeicSpace.Assessment.Application.Practice.Queries.GetPracticeSets;

/// <param name="Status">Only applied for content managers; learners always get published sets.</param>
public sealed record GetPracticeSetsQuery(
    int Page = 1,
    int PageSize = 20,
    ToeicPart? Part = null,
    PracticeSetKind? Kind = null,
    int? Level = null,
    ContentStatus? Status = null,
    string? Search = null) : IRequest<PaginatedResult<PracticeSetDto>>;
