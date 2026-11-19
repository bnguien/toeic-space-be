namespace ToeicSpace.Assessment.Application.Services.Attempts;

/// <summary>
/// The parts an attempt covers, stored as "1,2,3" in <see cref="ToeicAttempt.SelectedParts"/>.
/// </summary>
public static class AttemptParts
{
    public static IReadOnlyList<ToeicPart> All { get; } = Enum.GetValues<ToeicPart>();

    public static string Format(IEnumerable<ToeicPart> parts)
        => string.Join(",", parts.Distinct().Order().Select(part => (int)part));

    public static IReadOnlyList<ToeicPart> Parse(string? value)
        => string.IsNullOrWhiteSpace(value)
            ? []
            : value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(item => int.TryParse(item, out var number) ? number : 0)
                .Where(number => Enum.IsDefined(typeof(ToeicPart), number))
                .Select(number => (ToeicPart)number)
                .Distinct()
                .Order()
                .ToList();
}
