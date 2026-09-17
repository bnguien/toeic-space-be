using Microsoft.Extensions.Logging;
using ToeicSpace.Identity.Application.Interfaces.Persistence;
using ToeicSpace.Identity.Application.Options;

namespace ToeicSpace.Identity.Application.Features.CleanupInactiveAccounts.Commands;

public sealed class CleanupInactiveAccountsHandler
    : IRequestHandler<CleanupInactiveAccountsCommand, int>
{
    private readonly IUserRepository _userRepository;
    private readonly InactiveAccountCleanupOptions _options;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<CleanupInactiveAccountsHandler> _logger;

    public CleanupInactiveAccountsHandler(
        IUserRepository userRepository,
        InactiveAccountCleanupOptions options,
        TimeProvider timeProvider,
        ILogger<CleanupInactiveAccountsHandler> logger)
    {
        _userRepository = userRepository;
        _options = options;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public async Task<int> Handle(
        CleanupInactiveAccountsCommand request,
        CancellationToken cancellationToken)
    {
        var cutoffUtc = _timeProvider.GetUtcNow().UtcDateTime
            .Subtract(TimeSpan.FromDays(
                _options.InactiveAccountRetentionDays));
        var inactiveUsers = await _userRepository.GetInactiveOlderThanAsync(
            cutoffUtc,
            cancellationToken);

        if (inactiveUsers.Count == 0)
        {
            return 0;
        }

        await _userRepository.DeleteRangeAsync(
            inactiveUsers,
            cancellationToken);
        await _userRepository.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Deleted {DeletedCount} inactive accounts created before {CutoffUtc}",
            inactiveUsers.Count,
            cutoffUtc);

        return inactiveUsers.Count;
    }
}
