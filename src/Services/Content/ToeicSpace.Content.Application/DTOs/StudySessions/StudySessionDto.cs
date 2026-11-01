using ToeicSpace.Content.Application.DTOs.Vocabulary;
using ToeicSpace.Content.Domain.Enums;

namespace ToeicSpace.Content.Application.DTOs.StudySessions;

/// <summary>
/// Full session state. <paramref name="Items"/> is the queue in current order (re-queued cards included);
/// <paramref name="Summary"/> is set once completed; <paramref name="Review"/> only on an answer response.
/// </summary>
public sealed record StudySessionDto(Guid Id, StudyMode Mode, StudySourceDto Source, int Position, int Total,
    int CorrectCount, int ReviewCount, StudySessionStatus Status, DateTime StartedAt, DateTime UpdatedAt,
    DateTime? CompletedAt, IReadOnlyList<StudyItemDto> Items, IReadOnlyList<SessionResultDto> Results,
    SessionSummaryDto? Summary, ReviewResultDto? Review = null);
