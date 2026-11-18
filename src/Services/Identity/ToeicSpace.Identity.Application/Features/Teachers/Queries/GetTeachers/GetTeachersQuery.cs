using ToeicSpace.Identity.Application.DTOs.Teachers;


namespace ToeicSpace.Identity.Application.Features.Teachers.Queries.GetTeachers;

public sealed record GetTeachersQuery(string? Search, int Page = 1, int PageSize = 20)
    : IRequest<GetTeachersResult>;
