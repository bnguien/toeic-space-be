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

    /// <summary>
    /// More administrators created the same way (BootstrapAdmin:Additional:0:Email …), e.g. a tester's account.
    /// </summary>
    public List<BootstrapAccount> Additional { get; init; } = [];
}

public sealed class BootstrapAccount
{
    public string? Email { get; init; }

    public string? Password { get; init; }

    public string FullName { get; init; } = "Administrator";
}
