using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using ToeicSpace.Assessment.Application.Common.Behaviors;
using ToeicSpace.Assessment.Application.Common.Caching;
using ToeicSpace.Assessment.Application.Services;

namespace ToeicSpace.Assessment.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplicationServices(
        this IServiceCollection services)
    {
        var assembly = Assembly.GetExecutingAssembly();

        services.AddMediatR(config =>
            config.RegisterServicesFromAssembly(assembly));

        services.AddValidatorsFromAssembly(assembly);

        services.AddTransient(
            typeof(IPipelineBehavior<,>),
            typeof(ValidationBehavior<,>));

        services.AddScoped<QuestionReferenceGuard>();
        services.AddScoped<TestStructureInspector>();
        services.AddScoped<ContentCacheInvalidator>();

        return services;
    }
}
