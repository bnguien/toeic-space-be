namespace ToeicSpace.Identity.Application.Options;

public sealed class InactiveAccountCleanupOptions
{
    public const string SectionName = "Identity";

    public int InactiveAccountRetentionDays { get; init; } = 3;
}
