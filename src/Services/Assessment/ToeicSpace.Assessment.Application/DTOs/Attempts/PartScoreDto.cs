namespace ToeicSpace.Assessment.Application.DTOs.Attempts;

public sealed record PartScoreDto(
    int Part,
    int Total,
    int Answered,
    int Correct);
