using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using ToeicSpace.Assessment.Application.Common.Behaviors;
using ToeicSpace.Assessment.Application.Common.Caching;
using ToeicSpace.Assessment.Application.Services;
using ToeicSpace.Assessment.Application.Services.Attempts;

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

        services.AddScoped<AttemptAccess>();
        services.AddScoped<AttemptContentReader>();
        services.AddScoped<AttemptDocumentReader>();
        services.AddScoped<AttemptSourceReader>();
        services.AddScoped<AttemptGrader>();
        services.AddScoped<AnswerHistoryReader>();
        services.AddScoped<AttemptSummaryReader>();

        return services;
    }
}
