namespace ToeicSpace.Assessment.Application.DTOs.Attempts;

public sealed record RevealDto(
    IReadOnlyList<QuestionRevealDto> Questions,
    IReadOnlyList<PassageRevealDto> Passages)
{
    public static RevealDto Empty { get; } = new([], []);
}
