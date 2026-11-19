using ToeicSpace.Classroom.Application.Common.Pagination;
using ToeicSpace.Classroom.Application.DTOs.Classes;

namespace ToeicSpace.Classroom.Application.Features.Classes.Queries.GetMyClasses;

public sealed record GetMyClassesQuery(Guid TeacherId, int Page = 1, int PageSize = 20) : IRequest<PagedResult<ClassDto>>;
