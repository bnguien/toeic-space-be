using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using ToeicSpace.BuildingBlocks.Storage.Extensions;
using ToeicSpace.Content.Application.Interfaces;
using ToeicSpace.Content.Application.Interfaces.Repositories;
using ToeicSpace.Content.Application.Interfaces.Services;
using ToeicSpace.Content.Infrastructure.Persistence;
using ToeicSpace.Content.Infrastructure.Persistence.Repositories;
using ToeicSpace.Content.Infrastructure.Services;
using ToeicSpace.Content.Application.Interfaces.Services.Enrichment;
using ToeicSpace.Content.Application.Models.Enrichment;
using ToeicSpace.Content.Infrastructure.Services.Enrichment;
using ToeicSpace.Content.Infrastructure.Services.Enrichment.Gemini;
using ToeicSpace.Content.Infrastructure.Services.Pixabay;

namespace ToeicSpace.Content.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connection = configuration.GetConnectionString("ContentDatabase")
            ?? throw new InvalidOperationException("ContentDatabase connection string is required.");
        services.AddDbContext<ContentDbContext>(options => options.UseMySql(connection,
            new MySqlServerVersion(new Version(8, 4, 0))));
        services.AddScoped<ICourseRepository, CourseRepository>();
        services.AddScoped<ICourseModuleRepository, CourseModuleRepository>();
        services.AddScoped<ILessonRepository, LessonRepository>();
        services.AddScoped<ILessonUploadRepository, LessonUploadRepository>();
        services.AddScoped<IThumbnailUploadRepository, ThumbnailUploadRepository>();
        services.AddScoped<IVocabularyCatalogRepository, VocabularyCatalogRepository>();
        services.AddScoped<IVocabularyImportRepository, VocabularyImportRepository>();
        services.AddScoped<ILearnerProgressRepository, LearnerProgressRepository>();
        services.AddScoped<IReelBackgroundRepository, ReelBackgroundRepository>();
        services.AddScoped<ILearnerLibraryRepository, LearnerLibraryRepository>();
        services.AddScoped<IStudySessionRepository, StudySessionRepository>();
        services.AddScoped<IVocabularyPreferencesRepository, VocabularyPreferencesRepository>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddCloudflareR2Storage(configuration);
        services.AddScoped<IThumbnailObjectStore, R2ThumbnailObjectStore>();
        services.AddScoped<ILessonMediaStore, R2LessonMediaStore>();
        services.AddSingleton(TimeProvider.System);
        services.AddHostedService<ExpiredThumbnailCleanup>();
        services.AddHostedService<ExpiredLessonUploadCleanup>();
        services.Configure<PixabayOptions>(configuration.GetSection(PixabayOptions.SectionName));
        // Default HttpClient logging is removed: the Pixabay request URI carries the API key.
        services.AddHttpClient<PixabayVideoClient>(client => client.Timeout = TimeSpan.FromSeconds(30))
            .RemoveAllLoggers();
        services.AddHostedService<PixabayReelBackgroundRefresher>();
        services.AddVocabularyEnrichment(configuration);
        return services;
    }

    /// <summary>
    /// Enrichment tiers: cache (own DbContext scope per call), official catalog, dictionary (circuit breaker),
    /// translation chain and Gemini (key pool singleton). HttpClient request logging is removed for clients whose
    /// URLs or headers carry API keys.
    /// </summary>
    private static void AddVocabularyEnrichment(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton(configuration.GetSection(EnrichmentSettings.SectionName).Get<EnrichmentSettings>()
            ?? new EnrichmentSettings());
        services.Configure<GeminiOptions>(configuration.GetSection(GeminiOptions.SectionName));
        services.Configure<GoogleTranslateOptions>(configuration.GetSection(GoogleTranslateOptions.SectionName));
        services.AddScoped<IWordGlossCache, WordGlossCacheRepository>();
        services.AddScoped<ICuratedLexicon, CuratedLexicon>();
        services.AddSingleton<DictionaryCircuitBreaker>();
        services.AddHttpClient<IDictionaryProvider, FreeDictionaryProvider>(c => c.Timeout = TimeSpan.FromSeconds(5));
        services.AddHttpClient<ITranslationProvider, TranslationProvider>(c => c.Timeout = TimeSpan.FromSeconds(10))
            .RemoveAllLoggers();
        services.AddSingleton<GeminiKeyPool>();
        services.AddHttpClient<GeminiClient>(c => c.Timeout = TimeSpan.FromSeconds(30)).RemoveAllLoggers();
        services.AddScoped<IAiWordProvider, GeminiWordProvider>();
    }
}
