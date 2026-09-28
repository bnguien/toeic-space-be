using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ToeicSpace.Identity.Application.Interfaces.Persistence;
using ToeicSpace.Identity.Application.Interfaces.Security;
using ToeicSpace.Identity.Domain.Entities;
using ToeicSpace.Identity.Domain.Enums;

namespace ToeicSpace.Identity.Infrastructure.Services.Bootstrap;

/// <summary>
/// Registration only creates learners, so the first administrator comes from configuration.
/// An existing account is never modified.
/// </summary>
public sealed class AdminAccountBootstrapper : IHostedService
{
    private const int MinimumPasswordLength = 12;

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly BootstrapAdminOptions _options;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<AdminAccountBootstrapper> _logger;

    public AdminAccountBootstrapper(
        IServiceScopeFactory scopeFactory,
        BootstrapAdminOptions options,
        TimeProvider timeProvider,
        ILogger<AdminAccountBootstrapper> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_options.Email) || string.IsNullOrEmpty(_options.Password))
        {
            return;
        }

        if (!IsStrongPassword(_options.Password))
        {
            throw new InvalidOperationException(
                $"BootstrapAdmin:Password must have at least {MinimumPasswordLength} characters " +
                "and contain at least three of: lowercase, uppercase, digits, symbols.");
        }

        var email = _options.Email.Trim().ToLowerInvariant();

        await using var scope = _scopeFactory.CreateAsyncScope();
        var userRepository = scope.ServiceProvider.GetRequiredService<IUserRepository>();
        var passwordHasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();

        if (await userRepository.EmailExistsAsync(email, cancellationToken))
        {
            _logger.LogInformation("Bootstrap administrator already exists; nothing to create");
            return;
        }

        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var admin = new User
        {
            Id = Guid.NewGuid(),
            FullName = _options.FullName.Trim(),
            Email = email,
            PasswordHash = passwordHasher.Hash(_options.Password),
            EmailVerifiedAt = now,
            Role = UserRole.Admin,
            Status = UserStatus.Active,
            CreatedAt = now
        };

        await userRepository.AddAsync(admin, cancellationToken);
        await userRepository.SaveChangesAsync(cancellationToken);

        _logger.LogWarning(
            "Created bootstrap administrator {UserId}. Remove BootstrapAdmin:Password from the configuration",
            admin.Id);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private static bool IsStrongPassword(string password)
    {
        if (password.Length < MinimumPasswordLength)
        {
            return false;
        }

        var characterClasses = new[]
        {
            password.Any(char.IsLower),
            password.Any(char.IsUpper),
            password.Any(char.IsDigit),
            password.Any(character => !char.IsLetterOrDigit(character))
        };

        return characterClasses.Count(present => present) >= 3;
    }
}
