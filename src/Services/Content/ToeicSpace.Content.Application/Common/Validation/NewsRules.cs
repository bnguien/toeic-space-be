using ToeicSpace.Content.Application.DTOs.News;
using ToeicSpace.Content.Domain.Entities;

namespace ToeicSpace.Content.Application.Common.Validation;

/// <summary>Shared limits of the news learner endpoints.</summary>
public static class NewsRules
{
    public const int SlugMaxLength = 120;
    public const int SentenceIdMaxLength = 12;
    public const int DictationInputMaxLength = 2000;
    public const int MaxDictationEntries = 500;
    public const int MaxReadingSeconds = 1_000_000;
    public const string SentenceIdPattern = @"^p\d{1,3}s\d{1,3}$";

    public static readonly IReadOnlyList<string> Modes = ["english", "bilingual"];
    public static readonly IReadOnlyList<string> Layouts = ["inline", "columns"];
    public static readonly IReadOnlyList<string> HackModes = ["english", "vietnamese", "dictation"];

    public static IRuleBuilderOptions<T, string> NewsSlug<T>(this IRuleBuilder<T, string> rule) =>
        rule.NotEmpty().MaximumLength(SlugMaxLength);

    public static IRuleBuilderOptions<T, IReadOnlyList<string>?> KnownWords<T>(
        this IRuleBuilder<T, IReadOnlyList<string>?> rule) =>
        rule.Must(x => x is null || (x.Count <= NewsReading.MaxKnownWords
                && x.All(w => !string.IsNullOrWhiteSpace(w) && w.Length <= 60)))
            .WithMessage($"knownWords: at most {NewsReading.MaxKnownWords} words of 1-60 characters.");

    public static IRuleBuilderOptions<T, IReadOnlyDictionary<string, NewsDictationEntryInput>?> Dictation<T>(
        this IRuleBuilder<T, IReadOnlyDictionary<string, NewsDictationEntryInput>?> rule) =>
        rule.Must(x => x is null || (x.Count <= MaxDictationEntries && x.All(e =>
                System.Text.RegularExpressions.Regex.IsMatch(e.Key, SentenceIdPattern)
                && e.Value is not null && e.Value.Attempts is >= 0 and <= 10_000
                && (e.Value.Input?.Length ?? 0) <= DictationInputMaxLength)))
            .WithMessage("dictation: sentence ids p<n>s<n>, attempts 0..10000, input up to 2000 characters.");
}

public sealed class NewsReaderSettingsValidator : AbstractValidator<NewsReaderSettingsDto>
{
    public NewsReaderSettingsValidator()
    {
        RuleFor(x => x.Palette).MaximumLength(20);
        RuleFor(x => x.Mode).Must(x => x is null || NewsRules.Modes.Contains(x)).WithMessage("mode: english | bilingual.");
        RuleFor(x => x.Layout).Must(x => x is null || NewsRules.Layouts.Contains(x))
            .WithMessage("layout: inline | columns.");
        RuleFor(x => x.FontSize).InclusiveBetween(15, 26).When(x => x.FontSize.HasValue);
        RuleFor(x => x.PanelWidth).InclusiveBetween(200, 4000).When(x => x.PanelWidth.HasValue);
        RuleFor(x => x.Split).InclusiveBetween(30, 70).When(x => x.Split.HasValue);
        RuleFor(x => x.HackMode).Must(x => x is null || NewsRules.HackModes.Contains(x))
            .WithMessage("hackMode: english | vietnamese | dictation.");
        RuleFor(x => x.Voice).MaximumLength(200);
    }
}
