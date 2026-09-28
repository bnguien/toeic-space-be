using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection.Extensions;
using ToeicSpace.Assessment.Application.Data;
using ToeicSpace.Assessment.Application.Interfaces.Caching;
using ToeicSpace.Assessment.Infrastructure.Messaging.Outbox;
using ToeicSpace.Assessment.Infrastructure.Persistence;
using ToeicSpace.Assessment.Infrastructure.Persistence.Interceptors;
using ToeicSpace.Assessment.Infrastructure.Persistence.Search;
using ToeicSpace.Assessment.Infrastructure.Services.Caching;
using ToeicSpace.BuildingBlocks.Messaging;
using ToeicSpace.BuildingBlocks.Messaging.MassTransit;

namespace ToeicSpace.Assessment.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructureServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString =
            configuration.GetConnectionString("AssessmentDatabase")
            ?? throw new InvalidOperationException(
                "Connection string 'AssessmentDatabase' was not found. " +
                "Configure it via environment variable 'ConnectionStrings__AssessmentDatabase' " +
                "or in appsettings.json.");

        services.TryAddSingleton(TimeProvider.System);
        services.AddScoped<AuditableEntityInterceptor>();

        services.AddDbContext<AssessmentDbContext>((serviceProvider, options) =>
        {
            options.UseMySql(
                connectionString,
                new MySqlServerVersion(new Version(8, 4, 0)),
                mySqlOptions =>
                {
                    mySqlOptions.EnableRetryOnFailure(
                        maxRetryCount: 3,
                        maxRetryDelay: TimeSpan.FromSeconds(5),
                        errorNumbersToAdd: null);
                    mySqlOptions.CommandTimeout(30);
                });

            options.AddInterceptors(serviceProvider.GetRequiredService<AuditableEntityInterceptor>());

            // Soft-deleted questions/tests are intentionally hidden from required navigations
            // (practice set items, attempt answers), so this model warning is expected.
            options.ConfigureWarnings(warnings =>
                warnings.Ignore(CoreEventId.PossibleIncorrectRequiredNavigationWithQueryFilterInteractionWarning));

            var environment = configuration["ASPNETCORE_ENVIRONMENT"]
                ?? Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");

            if (string.Equals(environment, "Development", StringComparison.OrdinalIgnoreCase))
            {
                options.EnableDetailedErrors();
                options.EnableSensitiveDataLogging();
            }
        });

        services.AddScoped<IAssessmentDbContext>(serviceProvider =>
            serviceProvider.GetRequiredService<AssessmentDbContext>());

        services.AddScoped<IQuestionSearch, MySqlQuestionSearch>();

        AddContentCache(services, configuration);
        AddMessaging(services, configuration);

        return services;
    }

    private static void AddContentCache(
        IServiceCollection services,
        IConfiguration configuration)
    {
        var redisConnectionString = configuration.GetConnectionString("Redis");

        if (!string.IsNullOrWhiteSpace(redisConnectionString))
        {
            services.AddStackExchangeRedisCache(options =>
            {
                options.Configuration = redisConnectionString;
                options.InstanceName = "toeicspace:assessment:";
            });
        }
        else
        {
            services.AddDistributedMemoryCache();
        }

        var cacheSection = configuration.GetSection(ContentCacheOptions.SectionName);

        services.AddSingleton(new ContentCacheOptions
        {
            ExpirationMinutes = int.TryParse(cacheSection["ExpirationMinutes"], out var expirationMinutes)
                ? expirationMinutes
                : 10
        });

        services.AddSingleton<IContentCache, DistributedContentCache>();
    }

    private static void AddMessaging(
        IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddMessagingBus(configuration);

        // Events are written to the outbox table in the same transaction as the change
        // and published to the broker by the background dispatcher.
        services.Replace(ServiceDescriptor.Scoped<IIntegrationEventPublisher, OutboxIntegrationEventPublisher>());

        var outboxSection = configuration.GetSection(OutboxOptions.SectionName);

        services.AddSingleton(new OutboxOptions
        {
            PollingIntervalSeconds = int.TryParse(outboxSection["PollingIntervalSeconds"], out var interval) ? interval : 5,
            BatchSize = int.TryParse(outboxSection["BatchSize"], out var batchSize) ? batchSize : 50,
            MaxAttempts = int.TryParse(outboxSection["MaxAttempts"], out var maxAttempts) ? maxAttempts : 10
        });

        services.AddHostedService<OutboxDispatcher>();
    }
}
