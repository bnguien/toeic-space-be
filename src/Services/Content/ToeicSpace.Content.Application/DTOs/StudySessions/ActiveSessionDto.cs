using ToeicSpace.Content.Domain.Enums;

namespace ToeicSpace.Content.Application.DTOs.StudySessions;

/// <summary>Most recent active session, shown as "Học tiếp" on the home page.</summary>
public sealed record ActiveSessionDto(Guid Id, StudyMode Mode, StudySourceDto Source, int Position, int Total);
