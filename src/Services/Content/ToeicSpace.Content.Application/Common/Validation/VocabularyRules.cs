namespace ToeicSpace.Content.Application.Common.Validation;

/// <summary>Validation rules shared by vocabulary requests.</summary>
public static class VocabularyRules
{
    /// <summary>Minutes east of UTC, same convention as Identity statistics.</summary>
    public const int MinTzOffset = -720;
    public const int MaxTzOffset = 840;

    public static IRuleBuilderOptions<T, int> ValidTzOffset<T>(this IRuleBuilder<T, int> rule) =>
        rule.InclusiveBetween(MinTzOffset, MaxTzOffset)
            .WithMessage($"tzOffset must be between {MinTzOffset} and {MaxTzOffset} minutes.");
}
