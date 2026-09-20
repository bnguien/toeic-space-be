using System.Net;
using System.Net.Mail;

namespace ToeicSpace.Identity.Infrastructure.Services.Email;

public sealed class SmtpEmailSender : IEmailSender
{
    private readonly SmtpOptions _options;

    public SmtpEmailSender(SmtpOptions options)
    {
        _options = options;
    }

    public async Task SendAsync(
        string recipientEmail,
        string subject,
        string htmlBody,
        CancellationToken cancellationToken)
    {
        using var message = new MailMessage
        {
            From = new MailAddress(
                _options.SenderEmail,
                _options.SenderName),
            Subject = subject,
            Body = htmlBody,
            IsBodyHtml = true
        };
        message.To.Add(recipientEmail);

        using var smtpClient = new SmtpClient(_options.Server, _options.Port)
        {
            EnableSsl = true,
            UseDefaultCredentials = false,
            Credentials = new NetworkCredential(
                _options.SenderEmail,
                _options.Password)
        };

        await smtpClient.SendMailAsync(message, cancellationToken);
    }
}
