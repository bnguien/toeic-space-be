using Microsoft.Extensions.DependencyInjection;
using ToeicSpace.Content.Application.Common.Behaviors;
using ToeicSpace.Content.Application.Services;

namespace ToeicSpace.Content.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(
        this IServiceCollection services)
    {
        var assembly = typeof(DependencyInjection).Assembly;
        services.AddMediatR(config => config.RegisterServicesFromAssembly(assembly));
        services.AddValidatorsFromAssembly(assembly);
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
        services.AddScoped<LessonScopeService>();
        return services;
    }
}
