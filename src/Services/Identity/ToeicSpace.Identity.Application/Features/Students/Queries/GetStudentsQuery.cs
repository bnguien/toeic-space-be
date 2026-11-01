using ToeicSpace.Identity.Application.Features.Students.Results;

namespace ToeicSpace.Identity.Application.Features.Students.Queries;

public sealed record GetStudentsQuery(string? Search, int Page = 1, int PageSize = 20)
    : IRequest<GetStudentsResult>;
