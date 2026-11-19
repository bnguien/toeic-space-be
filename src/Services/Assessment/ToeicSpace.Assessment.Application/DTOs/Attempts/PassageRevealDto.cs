using System.Text.Json.Serialization;

namespace ToeicSpace.Assessment.Application.DTOs.Attempts;

/// <param name="Content">Full English content, including the Part 3/4 script.</param>
/// <param name="Translation">Vietnamese translation, when the source has one.</param>
public sealed record PassageRevealDto(
    Guid Id,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.Never)] string? Content,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.Never)] string? Translation);
