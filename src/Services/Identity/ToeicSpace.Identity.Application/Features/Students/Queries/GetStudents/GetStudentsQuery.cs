using ToeicSpace.Identity.Application.DTOs.Students;

namespace ToeicSpace.Identity.Application.Features.Students.Queries.GetStudents;

public sealed record GetStudentsQuery(string? Search, int Page = 1, int PageSize = 20)
    : IRequest<GetStudentsResult>;
