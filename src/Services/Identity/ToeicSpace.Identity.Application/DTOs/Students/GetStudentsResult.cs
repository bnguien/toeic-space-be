namespace ToeicSpace.Identity.Application.DTOs.Students;

public sealed record GetStudentsResult(IReadOnlyList<StudentResult> Items, int Page, int PageSize, int TotalCount);
