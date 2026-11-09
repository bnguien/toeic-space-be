namespace ToeicSpace.Content.Application.DTOs.Vocabulary;

public sealed record ReviewResultDto(Guid WordId, ProgressDto Progress, SchedulePreviewDto Preview, bool Duplicate);
