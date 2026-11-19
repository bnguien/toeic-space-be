using System.Text.Json.Serialization;

namespace ToeicSpace.Assessment.Application.DTOs.Attempts;

/// <summary>
/// Passage as shown while taking an attempt: Part 3/4 content (the script) is null and Part 6/7
/// content is the English text only, until the attempt is completed.
/// </summary>
public sealed record AttemptPassageDto(
    Guid Id,
    int Part,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.Never)] string? PassageType,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.Never)] string? Title,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.Never)] string? Content,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.Never)] string? AudioUrl,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.Never)] string? ImageUrl,
    int OrderIndex);
