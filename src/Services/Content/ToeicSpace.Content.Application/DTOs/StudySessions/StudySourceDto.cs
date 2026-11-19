using ToeicSpace.Content.Domain.Enums;

namespace ToeicSpace.Content.Application.DTOs.StudySessions;

/// <summary>
/// <paramref name="Title"/> is the lesson or folder name, "Từ sai" for mistakes; null for due, library and word-list
/// sessions.
/// </summary>
public sealed record StudySourceDto(StudySourceKind Kind, Guid? Id, string? Title);
