using ToeicSpace.Identity.Application.Features.Register.Commands;
using ToeicSpace.Identity.Application.Interfaces.Caching;
using ToeicSpace.Identity.Application.Interfaces.Messaging;
using ToeicSpace.Identity.Application.Messaging.Events;

namespace ToeicSpace.Identity.Application.Features.PasswordReset.Commands;

public sealed class RequestPasswordResetHandler(
    IPasswordResetStore resetStore,
    IIntegrationEventPublisher eventPublisher) : IRequestHandler<RequestPasswordResetCommand>
{
    public async Task Handle(RequestPasswordResetCommand request, CancellationToken cancellationToken)
    {
        var email = IdentityNormalizer.NormalizeEmail(request.Email);
        var requestId = await resetStore.BeginRequestAsync(email, cancellationToken);
        if (requestId is not null)
        {
            // Account lookup happens in the consumer, keeping responses identical for all emails.
            await eventPublisher.PublishAsync(
                new PasswordResetRequestedEvent(email, requestId), cancellationToken);
        }
    }
}
