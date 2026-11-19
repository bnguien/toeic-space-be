namespace ToeicSpace.Classroom.Application.Common.Pagination;

public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount);