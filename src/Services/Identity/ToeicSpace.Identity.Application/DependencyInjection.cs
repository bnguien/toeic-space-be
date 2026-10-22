using System.Reflection;
using ToeicSpace.Identity.Application.Common.Behaviors;
using ToeicSpace.Identity.Application.Common.Sessions;
using ToeicSpace.Identity.Application.Features.UserManagement.Common;

namespace ToeicSpace.Identity.Application;

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

        services.AddScoped<SessionIssuer>();
        services.AddScoped<ManagedUserReader>();

        return services;
    }
}
