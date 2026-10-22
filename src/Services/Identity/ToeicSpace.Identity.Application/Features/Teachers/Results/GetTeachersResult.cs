namespace ToeicSpace.Identity.Application.Features.Teachers.Results;

public sealed record TeacherResult(Guid Id, string FullName, string Email, string? AvatarUrl, string Status);

public sealed record GetTeachersResult(IReadOnlyList<TeacherResult> Items, int Page, int PageSize, int TotalCount);
