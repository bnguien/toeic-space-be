using MassTransit;
using ToeicSpace.Identity.Application.Extensions;
using ToeicSpace.Identity.Application.Interfaces.Caching;
using ToeicSpace.Identity.Application.Interfaces.Persistence;
using ToeicSpace.Identity.Application.Interfaces.Security;
using ToeicSpace.Identity.Application.Messaging.Events;
using ToeicSpace.Identity.Application.Options;
using ToeicSpace.Identity.Infrastructure.Services.Email;

namespace ToeicSpace.Identity.Infrastructure.Consumers;

public sealed class PasswordChangeRequestedConsumer(
    IUserRepository users,
    IPasswordResetStore otpStore,
    IOtpGenerator otpGenerator,
    IOtpHasher otpHasher,
    IRefreshTokenGenerator tokenGenerator,
    IEmailSender emailSender,
    EmailTemplateRenderer templateRenderer,
    PasswordResetOptions options,
    SmtpOptions smtpOptions) : IConsumer<PasswordChangeRequestedEvent>
{
    public async Task Consume(ConsumeContext<PasswordChangeRequestedEvent> context)
    {
        var user = await users.GetByIdAsync(context.Message.UserId, context.CancellationToken);
        if (user is null || !user.CanSignIn() || string.IsNullOrEmpty(user.PasswordHash))
        {
            return;
        }

        var otp = otpGenerator.Generate();
        if (!await otpStore.SetOtpAsync(user.Id.ToString("N"), context.Message.RequestId, user.Id,
                tokenGenerator.Hash(user.PasswordHash), otpHasher.Hash(otp), context.CancellationToken))
        {
            return;
        }

        var html = await templateRenderer.RenderPasswordChangeEmailAsync(user.FullName, otp,
            options.OtpLifetimeMinutes, smtpOptions.SupportEmail, context.CancellationToken);
        await emailSender.SendAsync(user.Email, "Xác minh đổi mật khẩu ToeicSpace", html,
            context.CancellationToken);
    }
}
