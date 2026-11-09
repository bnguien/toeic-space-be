namespace ToeicSpace.Content.Infrastructure.Services.Enrichment;

/// <summary>Section <c>GoogleTranslate</c> (env <c>GoogleTranslate__ApiKey</c>); without a key the paid API is skipped.</summary>
public sealed class GoogleTranslateOptions
{
    public const string SectionName = "GoogleTranslate";
    public string? ApiKey { get; set; }
}
