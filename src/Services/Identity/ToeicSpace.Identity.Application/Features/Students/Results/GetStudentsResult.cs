namespace ToeicSpace.Identity.Application.Features.Students.Results;

public sealed record StudentResult(Guid Id, string FullName, string Email, string? AvatarUrl, string Status);

public sealed record GetStudentsResult(IReadOnlyList<StudentResult> Items, int Page, int PageSize, int TotalCount);
