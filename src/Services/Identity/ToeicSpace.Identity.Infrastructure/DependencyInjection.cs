using Hangfire;
using Hangfire.MySql;
using MassTransit;
using Microsoft.Extensions.DependencyInjection.Extensions;
using StackExchange.Redis;
using ToeicSpace.Identity.Application.Interfaces.Caching;
using ToeicSpace.Identity.Application.Interfaces.Messaging;
using ToeicSpace.Identity.Application.Interfaces.Persistence;
using ToeicSpace.Identity.Application.Interfaces.Security;
using ToeicSpace.Identity.Application.Options;
using ToeicSpace.Identity.Infrastructure.Consumers;
using ToeicSpace.Identity.Infrastructure.Jobs;
using ToeicSpace.Identity.Infrastructure.Persistence;
using ToeicSpace.Identity.Application.Common.Sessions;
using ToeicSpace.Identity.Infrastructure.Persistence.Repositories;
using ToeicSpace.Identity.Infrastructure.Services.Bootstrap;
using ToeicSpace.Identity.Infrastructure.Services.Caching;
using ToeicSpace.Identity.Infrastructure.Services.Email;
using ToeicSpace.Identity.Infrastructure.Services.Messaging;
using ToeicSpace.Identity.Infrastructure.Services.Security;

namespace ToeicSpace.Identity.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructureServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString =
            configuration.GetConnectionString("IdentityDatabase")
            ?? throw new InvalidOperationException(
                "Connection string 'IdentityDatabase' was not found.");

        services.AddDbContext<IdentityDbContext>(options =>
            options.UseMySql(
                connectionString,
                new MySqlServerVersion(new Version(8, 4, 0))));

        services.AddHangfire(hangfire => hangfire
            .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
            .UseSimpleAssemblyNameTypeSerializer()
            .UseRecommendedSerializerSettings()
            .UseStorage(new MySqlStorage(
                connectionString,
                new MySqlStorageOptions
                {
                    PrepareSchemaIfNecessary = true,
                    TablesPrefix = "Hangfire"
                })));
        services.AddHangfireServer();

        var redisConnectionString =
            configuration.GetConnectionString("Redis")
            ?? throw new InvalidOperationException(
                "Connection string 'Redis' was not found.");

        var otpSection = configuration.GetSection(OtpOptions.SectionName);
        var otpOptions = new OtpOptions
        {
            HmacSecret = otpSection["HmacSecret"] ?? string.Empty,
            ChallengeTtlMinutes = int.TryParse(
                otpSection["ChallengeTtlMinutes"],
                out var challengeTtlMinutes)
                ? challengeTtlMinutes
                : 5,
            ResendCooldownSeconds = int.TryParse(
                otpSection["ResendCooldownSeconds"],
                out var resendCooldownSeconds)
                ? resendCooldownSeconds
                : 60
        };

        var identitySection = configuration.GetSection(
            InactiveAccountCleanupOptions.SectionName);
        var cleanupOptions = new InactiveAccountCleanupOptions
        {
            InactiveAccountRetentionDays = int.TryParse(
                identitySection["InactiveAccountRetentionDays"],
                out var inactiveAccountRetentionDays)
                ? inactiveAccountRetentionDays
                : 3
        };

        if (cleanupOptions.InactiveAccountRetentionDays <= 0)
        {
            throw new InvalidOperationException(
                "Identity:InactiveAccountRetentionDays must be greater than zero.");
        }

        var rabbitMqSection = configuration.GetSection(RabbitMqOptions.SectionName);
        var rabbitMqOptions = new RabbitMqOptions
        {
            Host = rabbitMqSection["Host"] ?? "localhost",
            VirtualHost = rabbitMqSection["VirtualHost"] ?? "/",
            Username = rabbitMqSection["Username"] ?? "guest",
            Password = rabbitMqSection["Password"] ?? "guest"
        };

        var smtpSection = configuration.GetSection(SmtpOptions.SectionName);
        var smtpOptions = new SmtpOptions
        {
            Server = smtpSection["Server"] ?? string.Empty,
            Port = int.TryParse(smtpSection["Port"], out var smtpPort)
                ? smtpPort
                : 587,
            SenderName = smtpSection["SenderName"] ?? "ToeicSpace",
            SenderEmail = smtpSection["SenderEmail"] ?? string.Empty,
            Password = smtpSection["Password"] ?? string.Empty,
            SupportEmail = smtpSection["SupportEmail"]
                ?? "support@toeicspace.com"
        };

        var jwtOptions = configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>()
            ?? new JwtOptions();
        var sessionOptions = configuration.GetSection(SessionOptions.SectionName).Get<SessionOptions>()
            ?? new SessionOptions();
        var loginLockoutOptions = configuration.GetSection(LoginLockoutOptions.SectionName).Get<LoginLockoutOptions>()
            ?? new LoginLockoutOptions();
        var bootstrapAdminOptions = configuration.GetSection(BootstrapAdminOptions.SectionName).Get<BootstrapAdminOptions>()
            ?? new BootstrapAdminOptions();

        if (sessionOptions.RefreshTokenLifetime <= TimeSpan.Zero
            || sessionOptions.IdleTimeout <= TimeSpan.Zero
            || sessionOptions.MaxActiveSessions <= 0)
        {
            throw new InvalidOperationException(
                "Session:RefreshTokenLifetime, Session:IdleTimeout and Session:MaxActiveSessions must be greater than zero.");
        }

        services.AddSingleton(otpOptions);
        services.AddSingleton(cleanupOptions);
        services.AddSingleton(smtpOptions);
        services.AddSingleton(jwtOptions);
        services.AddSingleton(sessionOptions);
        services.AddSingleton(loginLockoutOptions);
        services.AddSingleton(bootstrapAdminOptions);
        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton<IConnectionMultiplexer>(_ =>
            ConnectionMultiplexer.Connect(redisConnectionString));

        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();
        services.AddSingleton<IPasswordHasher, Pbkdf2PasswordHasher>();
        services.AddSingleton<IAccessTokenIssuer, JwtAccessTokenIssuer>();
        services.AddSingleton<IRefreshTokenGenerator, RefreshTokenGenerator>();
        services.AddSingleton<ILoginAttemptLimiter, RedisLoginAttemptLimiter>();
        services.AddHostedService<AdminAccountBootstrapper>();
        services.AddSingleton<IOtpGenerator, CryptographicOtpGenerator>();
        services.AddSingleton<IOtpHasher, HmacOtpHasher>();
        services.AddSingleton<IOtpChallengeStore, RedisOtpChallengeStore>();
        services.AddScoped<IIntegrationEventPublisher, MassTransitIntegrationEventPublisher>();
        services.AddScoped<IEmailSender, SmtpEmailSender>();
        services.AddScoped<InactiveAccountCleanupJob>();
        services.AddSingleton<EmailTemplateRenderer>();
        services.AddHostedService<HangfireRecurringJobRegistrar>();

        services.AddMassTransit(configurator =>
        {
            configurator.AddConsumer<UserRegistrationOtpRequestedConsumer>();

            configurator.UsingRabbitMq((context, rabbitMq) =>
            {
                rabbitMq.Host(
                    rabbitMqOptions.Host,
                    rabbitMqOptions.VirtualHost,
                    host =>
                    {
                        host.Username(rabbitMqOptions.Username);
                        host.Password(rabbitMqOptions.Password);
                    });

                rabbitMq.ConfigureEndpoints(context);
            });
        });

        return services;
    }
}
