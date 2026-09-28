namespace ToeicSpace.Identity.Infrastructure.Services.Bootstrap;

/// <summary>
/// Creates the first administrator at startup. Supply the values through user-secrets or
/// environment variables only, and remove the password once the account exists.
/// </summary>
public sealed class BootstrapAdminOptions
{
    public const string SectionName = "BootstrapAdmin";

    public string? Email { get; init; }

    public string? Password { get; init; }

    public string FullName { get; init; } = "Administrator";
}
