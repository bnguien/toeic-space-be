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
        services.AddScoped<LibraryWordService>();
        services.AddScoped<PersonalWordOverlay>();
        services.AddScoped<StudyQueueService>();
        services.AddScoped<ReviewRecorder>();
        services.AddScoped<StudySessionPresenter>();
        services.AddScoped<ToeicSpace.Content.Application.Services.Enrichment.WordEnricher>();
        return services;
    }
}
