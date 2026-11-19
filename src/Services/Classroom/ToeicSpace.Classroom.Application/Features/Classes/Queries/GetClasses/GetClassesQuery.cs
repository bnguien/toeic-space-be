using ToeicSpace.Classroom.Application.Common.Pagination;
using ToeicSpace.Classroom.Application.DTOs.Classes;
using ToeicSpace.Classroom.Domain.Entities;
using ToeicSpace.Classroom.Domain.Enums;

namespace ToeicSpace.Classroom.Application.Features.Classes.Queries.GetClasses;

public sealed record GetClassesQuery(string? Search, ClassStatus? Status, Guid? TeacherId, DateOnly? StartDateFrom, DateOnly? StartDateTo, int Page = 1, int PageSize = 20)
    : IRequest<PagedResult<ClassDto>>;
