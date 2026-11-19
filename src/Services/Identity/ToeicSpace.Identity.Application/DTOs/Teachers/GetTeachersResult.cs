namespace ToeicSpace.Identity.Application.DTOs.Teachers;

public sealed record GetTeachersResult(IReadOnlyList<TeacherResult> Items, int Page, int PageSize, int TotalCount);
