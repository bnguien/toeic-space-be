using Microsoft.Extensions.DependencyInjection;
using ToeicSpace.Content.Application.Common.Behaviors;
using ToeicSpace.Content.Application.Features.GrammarAttempts;
using ToeicSpace.Content.Application.Services;
using ToeicSpace.Content.Application.Services.Grammar;

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
        services.AddScoped<NewsReadingService>();
        services.AddScoped<ToeicSpace.Content.Application.Services.Enrichment.WordEnricher>();
        services.AddSingleton<DictationProofService>();
        services.AddVideoServices();
        services.AddGrammarServices();
        return services;
    }

    /// <summary>Grammar roadmap (PBL-107): content cache (dropped after each import) and per-request learning services.</summary>
    private static void AddGrammarServices(this IServiceCollection services)
    {
        services.AddSingleton<GrammarContentCache>();
        services.AddScoped<GrammarContentProvider>();
        services.AddScoped<GrammarProgressService>();
        services.AddScoped<GrammarResultRecorder>();
        services.AddScoped<GrammarAttemptResolver>();
        services.AddScoped<GrammarSingleQuestions>();
    }

    /// <summary>Bilingual video (PBL-107): singletons hold process caches; the rest is per request/job scope.</summary>
    private static void AddVideoServices(this IServiceCollection services)
    {
        services.AddSingleton<Services.Video.VideoCatalogCache>();
        services.AddSingleton<Services.Video.VideoQuota>();
        services.AddSingleton<Services.Video.VideoWordLevelProvider>();
        services.AddScoped<Services.Video.SubtitleStore>();
        services.AddScoped<Services.Video.SubtitleTranslator>();
        services.AddScoped<Services.Video.SubtitlePreparationService>();
        services.AddScoped<Services.Video.CatalogVideoStatusService>();
        services.AddScoped<Services.Video.VideoGlossService>();
        services.AddScoped<Services.Video.WordDetailService>();
        services.AddScoped<Services.Video.VideoExerciseCues>();
    }
}
