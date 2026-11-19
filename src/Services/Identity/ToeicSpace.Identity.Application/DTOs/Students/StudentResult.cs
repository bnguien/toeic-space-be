namespace ToeicSpace.Identity.Application.DTOs.Students;

public sealed record StudentResult(Guid Id, string FullName, string Email, string? AvatarUrl, string Status);
