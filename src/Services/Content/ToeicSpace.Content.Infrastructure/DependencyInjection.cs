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
using ToeicSpace.Content.Application.Models.News;
using ToeicSpace.Content.Infrastructure.Services.News;
using ToeicSpace.Content.Infrastructure.Services.Pixabay;
using ToeicSpace.Content.Infrastructure.Services.Speech;
using ToeicSpace.Content.Application.Models.Dictation;
using ToeicSpace.Content.Application.Interfaces.Services.Video;
using ToeicSpace.Content.Infrastructure.Services.Video;
using ToeicSpace.Content.Infrastructure.Services.Grammar;

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
        services.AddScoped<IPublicCourseRepository, PublicCourseRepository>();
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
        services.AddNews(configuration);
        services.AddDictation(configuration);
        services.AddVideoInfrastructure();
        services.AddGrammar(configuration);
        return services;
    }

    /// <summary>Bilingual news: articles, readings and the cover store (<c>News</c> section).</summary>
    private static void AddNews(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton(configuration.GetSection(NewsSettings.SectionName).Get<NewsSettings>() ?? new NewsSettings());
        services.AddScoped<INewsArticleRepository, NewsArticleRepository>();
        services.AddScoped<INewsReadingRepository, NewsReadingRepository>();
        services.AddSingleton<INewsCoverStore, FileSystemNewsCoverStore>();
    }

    /// <summary>
    /// Dictation repositories and the shared speech service: edge-tts websocket client, disk-cached synthesizer
    /// (singletons) and the background queue that renders the next sentences ahead of time.
    /// </summary>
    private static void AddDictation(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton(configuration.GetSection(DictationSettings.SectionName).Get<DictationSettings>()
            ?? new DictationSettings());
        services.AddScoped<IDictationCatalogRepository, DictationCatalogRepository>();
        services.AddScoped<IDictationProgressRepository, DictationProgressRepository>();
        services.AddScoped<IDictationImportRepository, DictationImportRepository>();
        services.Configure<SpeechOptions>(configuration.GetSection(SpeechOptions.SectionName));
        services.AddSingleton<EdgeTtsClient>();
        services.AddSingleton<ISpeechSynthesizer, CachedSpeechSynthesizer>();
        services.AddSingleton<SpeechPrewarmQueue>();
        services.AddSingleton<ISpeechPrewarmQueue>(provider => provider.GetRequiredService<SpeechPrewarmQueue>());
        services.AddHostedService<SpeechPrewarmWorker>();
    }

    /// <summary>Grammar roadmap (PBL-107): content, learning and comment repositories plus the guest attempt tickets.</summary>
    private static void AddGrammar(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<IGrammarContentRepository, GrammarContentRepository>();
        services.AddScoped<IGrammarLearningRepository, GrammarLearningRepository>();
        services.AddScoped<IGrammarCommentRepository, GrammarCommentRepository>();
        services.Configure<GrammarOptions>(configuration.GetSection(GrammarOptions.SectionName));
        services.AddSingleton<IGuestAttemptTokens, HmacGuestAttemptTokens>();
    }

    /// <summary>
    /// Bilingual video (PBL-107): repositories, process cache, embedded word levels, YouTube/oEmbed/dictionary clients
    /// (browser User-Agent) and the single-flight subtitle queue with its background worker.
    /// </summary>
    private static void AddVideoInfrastructure(this IServiceCollection services)
    {
        services.AddMemoryCache();
        services.AddScoped<IVideoCatalogRepository, VideoCatalogRepository>();
        services.AddScoped<IVideoSubtitleRepository, VideoSubtitleRepository>();
        services.AddScoped<IVideoWatchRepository, VideoWatchRepository>();
        services.AddScoped<IVideoLearnerStateRepository, VideoLearnerStateRepository>();
        services.AddScoped<IVideoLexiconRepository, VideoLexiconRepository>();
        services.AddSingleton<IVideoCache, MemoryVideoCache>();
        services.AddSingleton<IVideoWordLevelSource, EmbeddedVideoWordLevelSource>();
        services.AddSingleton<VideoSubtitleQueue>();
        services.AddSingleton<IVideoSubtitlePreparer>(sp => sp.GetRequiredService<VideoSubtitleQueue>());
        services.AddHostedService<VideoSubtitleWorker>();
        static void Browser(HttpClient client)
        {
            client.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", TranslationProvider.BrowserUserAgent);
            client.DefaultRequestHeaders.TryAddWithoutValidation("Accept-Language", "en-US,en;q=0.9");
        }
        // YoutubeExplode sets its own client headers (a second User-Agent makes the player API answer 400).
        services.AddHttpClient<IYouTubeSubtitleClient, YouTubeSubtitleClient>(c => c.Timeout = TimeSpan.FromSeconds(40));
        services.AddHttpClient<IVideoEmbedProbe, VideoEmbedProbe>(c => { Browser(c); c.Timeout = TimeSpan.FromSeconds(15); });
        services.AddHttpClient<IWordDetailDictionary, FreeWordDetailDictionary>(c => { Browser(c); c.Timeout = TimeSpan.FromSeconds(8); });
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
