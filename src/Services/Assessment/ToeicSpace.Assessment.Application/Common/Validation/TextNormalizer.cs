namespace ToeicSpace.Assessment.Application.Common.Validation;

public static class TextNormalizer
{
    /// <summary>
    /// Trims the value and converts empty strings to null so optional columns stay NULL.
    /// </summary>
    public static string? NullIfEmpty(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
