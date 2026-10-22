using System.Reflection;
using ToeicSpace.Classroom.Application.Common.Behaviors;
using ToeicSpace.Classroom.Application.Services;

namespace ToeicSpace.Classroom.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        var assembly = Assembly.GetExecutingAssembly();
        services.AddMediatR(config => config.RegisterServicesFromAssembly(assembly));
        services.AddValidatorsFromAssembly(assembly);
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
        services.AddScoped<TeacherEligibilityService>();
        services.AddScoped<ClassActivationService>();
        services.AddScoped<ClassRestorationService>();
        return services;
    }
}
