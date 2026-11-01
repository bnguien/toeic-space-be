using ToeicSpace.Classroom.Application.Common.Pagination;
using ToeicSpace.Classroom.Application.DTOs.Classes;
using ToeicSpace.Classroom.Domain.Enums;

namespace ToeicSpace.Classroom.Application.Features.Classes.Queries.GetMyEnrolledClasses;

public sealed record GetMyEnrolledClassesQuery(Guid StudentId, string? Search, ClassStatus? Status,
    int Page = 1, int PageSize = 20) : IRequest<PagedResult<ClassDto>>;
