using MassTransit;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ToeicSpace.BuildingBlocks.Messaging.MassTransit;

public static class DependencyInjection
{
    private static readonly string[] InMemoryHostAliases = ["in-memory", "disabled"];

    public static IServiceCollection AddMessagingBus(
        this IServiceCollection services,
        IConfiguration configuration,
        Action<IBusRegistrationConfigurator>? configureConsumers = null)
    {
        services.AddScoped<IIntegrationEventPublisher, MassTransitEventPublisher>();

        var rabbitMqHost = configuration["RabbitMQ:Host"]
            ?? configuration["RabbitMQ__Host"];

        var useInMemoryBus = string.IsNullOrWhiteSpace(rabbitMqHost)
            || InMemoryHostAliases.Contains(rabbitMqHost, StringComparer.OrdinalIgnoreCase);

        if (useInMemoryBus && string.IsNullOrWhiteSpace(rabbitMqHost) && !IsDevelopment(configuration))
        {
            // Without a broker, events would silently stay in-process. Only acceptable for local development
            // or when explicitly requested with RabbitMQ:Host = "in-memory".
            throw new InvalidOperationException(
                "RabbitMQ:Host is not configured. Set 'RabbitMQ__Host' or use 'in-memory' explicitly.");
        }

        services.AddMassTransit(configurator =>
        {
            configureConsumers?.Invoke(configurator);

            if (!useInMemoryBus)
            {
                var username = configuration["RabbitMQ:Username"] ?? configuration["RabbitMQ__Username"] ?? "guest";
                var password = configuration["RabbitMQ:Password"] ?? configuration["RabbitMQ__Password"] ?? "guest";
                var virtualHost = configuration["RabbitMQ:VirtualHost"] ?? configuration["RabbitMQ__VirtualHost"] ?? "/";

                configurator.UsingRabbitMq((context, cfg) =>
                {
                    cfg.Host(rabbitMqHost, virtualHost, h =>
                    {
                        h.Username(username);
                        h.Password(password);
                    });

                    cfg.ConfigureEndpoints(context);
                });
            }
            else
            {
                configurator.UsingInMemory((context, cfg) =>
                {
                    cfg.ConfigureEndpoints(context);
                });
            }
        });

        return services;
    }

    private static bool IsDevelopment(IConfiguration configuration)
    {
        var environment = configuration["ASPNETCORE_ENVIRONMENT"]
            ?? configuration["DOTNET_ENVIRONMENT"]
            ?? Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")
            ?? Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT");

        return string.Equals(environment, "Development", StringComparison.OrdinalIgnoreCase);
    }
}
