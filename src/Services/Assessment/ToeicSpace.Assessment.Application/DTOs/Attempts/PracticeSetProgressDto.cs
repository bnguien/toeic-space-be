using System.Text.Json.Serialization;

namespace ToeicSpace.Assessment.Application.DTOs.Attempts;

/// <summary>
/// Progress over the distinct source questions of a set; correctness comes from the latest graded answer.
/// </summary>
public sealed record PracticeSetProgressDto(
    Guid PracticeSetId,
    int TotalQuestions,
    int AnsweredQuestions,
    int CorrectQuestions,
    int WrongQuestions,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.Never)] Guid? ActiveAttemptId);
