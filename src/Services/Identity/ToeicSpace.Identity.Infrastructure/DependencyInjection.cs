using MassTransit;
using StackExchange.Redis;
using ToeicSpace.Identity.Application.Interfaces.Caching;
using ToeicSpace.Identity.Application.Interfaces.Messaging;
using ToeicSpace.Identity.Application.Interfaces.Persistence;
using ToeicSpace.Identity.Application.Interfaces.Security;
using ToeicSpace.Identity.Infrastructure.Consumers;
using ToeicSpace.Identity.Infrastructure.Persistence;
using ToeicSpace.Identity.Infrastructure.Persistence.Repositories;
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
                : 5
        };

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

        services.AddSingleton(otpOptions);
        services.AddSingleton(smtpOptions);
        services.AddSingleton<IConnectionMultiplexer>(_ =>
            ConnectionMultiplexer.Connect(redisConnectionString));

        services.AddScoped<IUserRepository, UserRepository>();
        services.AddSingleton<IPasswordHasher, Pbkdf2PasswordHasher>();
        services.AddSingleton<IOtpGenerator, CryptographicOtpGenerator>();
        services.AddSingleton<IOtpHasher, HmacOtpHasher>();
        services.AddSingleton<IOtpChallengeStore, RedisOtpChallengeStore>();
        services.AddScoped<IIntegrationEventPublisher, MassTransitIntegrationEventPublisher>();
        services.AddScoped<IEmailSender, SmtpEmailSender>();
        services.AddSingleton<EmailTemplateRenderer>();

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
