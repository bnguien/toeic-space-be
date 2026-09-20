using MassTransit;
using Microsoft.Extensions.Logging;
using ToeicSpace.Identity.Application.Messaging.Events;
using ToeicSpace.Identity.Infrastructure.Services.Email;
using ToeicSpace.Identity.Infrastructure.Services.Security;

namespace ToeicSpace.Identity.Infrastructure.Consumers;

public sealed class UserRegistrationOtpRequestedConsumer
    : IConsumer<UserRegistrationOtpRequestedEvent>
{
    private readonly IEmailSender _emailSender;
    private readonly EmailTemplateRenderer _templateRenderer;
    private readonly OtpOptions _otpOptions;
    private readonly SmtpOptions _smtpOptions;
    private readonly ILogger<UserRegistrationOtpRequestedConsumer> _logger;

    public UserRegistrationOtpRequestedConsumer(
        IEmailSender emailSender,
        EmailTemplateRenderer templateRenderer,
        OtpOptions otpOptions,
        SmtpOptions smtpOptions,
        ILogger<UserRegistrationOtpRequestedConsumer> logger)
    {
        _emailSender = emailSender;
        _templateRenderer = templateRenderer;
        _otpOptions = otpOptions;
        _smtpOptions = smtpOptions;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<UserRegistrationOtpRequestedEvent> context)
    {
        var htmlBody = await _templateRenderer.RenderVerificationEmailAsync(
            context.Message.FullName,
            context.Message.Otp,
            _otpOptions.ChallengeTtlMinutes,
            _smtpOptions.SupportEmail,
            context.CancellationToken);

        await _emailSender.SendAsync(
            context.Message.Email,
            "Mã xác thực tài khoản ToeicSpace",
            htmlBody,
            context.CancellationToken);

        _logger.LogInformation(
            "Sent an email verification message to {Email}",
            context.Message.Email);
    }
}
