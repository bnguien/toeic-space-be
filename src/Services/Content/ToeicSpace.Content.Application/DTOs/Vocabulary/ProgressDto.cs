using ToeicSpace.Content.Domain.Enums;

namespace ToeicSpace.Content.Application.DTOs.Vocabulary;

public sealed record ProgressDto(WordStatus Status, int IntervalDays, int Repetitions, DateOnly? DueOn, bool IsDue,
    DateTime? LastReviewedAt);
