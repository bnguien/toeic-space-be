using System.Text.Json.Serialization;

namespace ToeicSpace.Assessment.Application.DTOs.Attempts;

/// <param name="Generation">Lifecycle fence the next start request must echo as expectedGeneration.</param>
public sealed record ActivePracticeAttemptDto(
    long Generation,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.Never)] Guid? AttemptId);
