using System.Globalization;
using System.Net;

namespace ToeicSpace.Identity.Infrastructure.Services.Email;

public sealed class EmailTemplateRenderer
{
    private const string VerificationTemplatePath =
        "Services/Email/Templates/EmailVerification.html";

    public Task<string> RenderVerificationEmailAsync(
        string fullName,
        string otp,
        int expirationMinutes,
        string supportEmail,
        CancellationToken cancellationToken = default)
        => RenderAsync(VerificationTemplatePath, fullName, otp, expirationMinutes, supportEmail, cancellationToken);

    public Task<string> RenderPasswordResetEmailAsync(
        string fullName, string otp, int expirationMinutes, string supportEmail,
        CancellationToken cancellationToken = default)
        => RenderAsync("Services/Email/Templates/PasswordReset.html", fullName, otp,
            expirationMinutes, supportEmail, cancellationToken);

    private static async Task<string> RenderAsync(
        string relativePath, string fullName, string otp, int expirationMinutes, string supportEmail,
        CancellationToken cancellationToken)
    {
        var templatePath = Path.Combine(
            AppContext.BaseDirectory,
            relativePath);

        var template = await File.ReadAllTextAsync(
            templatePath,
            cancellationToken);

        return template
            .Replace(
                "{{FullName}}",
                WebUtility.HtmlEncode(fullName),
                StringComparison.Ordinal)
            .Replace(
                "{{Otp}}",
                WebUtility.HtmlEncode(otp),
                StringComparison.Ordinal)
            .Replace(
                "{{ExpirationMinutes}}",
                expirationMinutes.ToString(CultureInfo.InvariantCulture),
                StringComparison.Ordinal)
            .Replace(
                "{{SupportEmail}}",
                WebUtility.HtmlEncode(supportEmail),
                StringComparison.Ordinal)
            .Replace(
                "{{CurrentYear}}",
                DateTime.UtcNow.Year.ToString(CultureInfo.InvariantCulture),
                StringComparison.Ordinal);
    }
}
