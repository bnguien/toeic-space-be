using MassTransit;
using ToeicSpace.Identity.Application.Extensions;
using ToeicSpace.Identity.Application.Interfaces.Caching;
using ToeicSpace.Identity.Application.Interfaces.Persistence;
using ToeicSpace.Identity.Application.Interfaces.Security;
using ToeicSpace.Identity.Application.Messaging.Events;
using ToeicSpace.Identity.Application.Options;
using ToeicSpace.Identity.Infrastructure.Services.Email;

namespace ToeicSpace.Identity.Infrastructure.Consumers;

public sealed class PasswordResetRequestedConsumer(
    IUserRepository users,
    IPasswordResetStore resetStore,
    IOtpGenerator otpGenerator,
    IOtpHasher otpHasher,
    IRefreshTokenGenerator tokenGenerator,
    IEmailSender emailSender,
    EmailTemplateRenderer templateRenderer,
    PasswordResetOptions options,
    SmtpOptions smtpOptions) : IConsumer<PasswordResetRequestedEvent>
{
    public async Task Consume(ConsumeContext<PasswordResetRequestedEvent> context)
    {
        var user = await users.GetByEmailAsync(context.Message.Email, context.CancellationToken);
        if (user is null || !user.CanSignIn() || string.IsNullOrEmpty(user.PasswordHash))
        {
            return;
        }

        var otp = otpGenerator.Generate();
        if (!await resetStore.SetOtpAsync(context.Message.Email, context.Message.RequestId, user.Id,
                tokenGenerator.Hash(user.PasswordHash), otpHasher.Hash(otp), context.CancellationToken))
        {
            return;
        }

        var html = await templateRenderer.RenderPasswordResetEmailAsync(user.FullName, otp,
            options.OtpLifetimeMinutes, smtpOptions.SupportEmail, context.CancellationToken);
        await emailSender.SendAsync(user.Email, "Đặt lại mật khẩu ToeicSpace", html, context.CancellationToken);
    }
}
