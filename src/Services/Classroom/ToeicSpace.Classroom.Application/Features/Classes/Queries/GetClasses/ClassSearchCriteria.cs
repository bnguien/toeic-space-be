using ToeicSpace.Classroom.Domain.Enums;

namespace ToeicSpace.Classroom.Application.Features.Classes.Queries.GetClasses;

public sealed record ClassSearchCriteria(
    string? Search,
    ClassStatus? Status,
    Guid? TeacherId,
    DateOnly? StartDateFrom,
    DateOnly? StartDateTo,
    int Page = 1,
    int PageSize = 20);
