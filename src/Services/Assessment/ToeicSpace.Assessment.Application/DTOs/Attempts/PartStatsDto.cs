namespace ToeicSpace.Assessment.Application.DTOs.Attempts;

public sealed record PartStatsDto(
    int Part,
    int Answered,
    int Correct);
