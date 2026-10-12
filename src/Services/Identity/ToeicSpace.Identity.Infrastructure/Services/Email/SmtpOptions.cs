namespace ToeicSpace.Identity.Infrastructure.Services.Email;

public sealed class SmtpOptions
{
    public const string SectionName = "Smtp";

    public string Server { get; init; } = string.Empty;

    public int Port { get; init; } = 587;

    public string SenderName { get; init; } = "ToeicSpace";

    public string SenderEmail { get; init; } = string.Empty;

    public string Password { get; init; } = string.Empty;

    public string SupportEmail { get; init; } = "support@toeicspace.com";
}
