using ToeicSpace.Identity.Application.Features.Teachers.Results;

namespace ToeicSpace.Identity.Application.Features.Teachers.Queries;

public sealed record GetTeachersQuery(string? Search, int Page = 1, int PageSize = 20)
    : IRequest<GetTeachersResult>;
