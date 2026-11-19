using ToeicSpace.Classroom.Application.Common.Pagination;
using ToeicSpace.Classroom.Application.DTOs.Classes;

namespace ToeicSpace.Classroom.Application.Features.Classes.Queries.GetDeletedClasses;

public sealed record GetDeletedClassesQuery(string? Search, int Page = 1, int PageSize = 20)
    : IRequest<PagedResult<ClassDto>>;
