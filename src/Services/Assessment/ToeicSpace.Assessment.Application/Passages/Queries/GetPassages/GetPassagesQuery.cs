using ToeicSpace.Assessment.Application.Dtos;
using ToeicSpace.BuildingBlocks.Pagination;

namespace ToeicSpace.Assessment.Application.Passages.Queries.GetPassages;

/// <param name="BankOnly">True: only passages not owned by a test. False: only test passages. Null: both.</param>
public sealed record GetPassagesQuery(
    int Page = 1,
    int PageSize = 20,
    Guid? TestId = null,
    bool? BankOnly = null,
    ToeicPart? Part = null) : IRequest<PaginatedResult<PassageDto>>;
