using ToeicSpace.Content.Domain.Enums;

namespace ToeicSpace.Content.Application.DTOs.StudySessions;

public sealed record SessionResultDto(Guid WordId, ReviewGrade Grade);
