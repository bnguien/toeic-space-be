using MediatR;
using ToeicSpace.Identity.Application.Features.CleanupInactiveAccounts.Commands;

namespace ToeicSpace.Identity.Infrastructure.Jobs;

public sealed class InactiveAccountCleanupJob
{
    private readonly IMediator _mediator;

    public InactiveAccountCleanupJob(IMediator mediator)
    {
        _mediator = mediator;
    }

    public async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        await _mediator.Send(
            new CleanupInactiveAccountsCommand(),
            cancellationToken);
    }
}
