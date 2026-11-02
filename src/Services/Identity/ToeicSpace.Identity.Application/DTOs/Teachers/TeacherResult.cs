namespace ToeicSpace.Identity.Application.DTOs.Teachers;

public sealed record TeacherResult(Guid Id, string FullName, string Email, string? AvatarUrl, string Status);
