using ToeicSpace.Content.Application.DTOs.News;
using ToeicSpace.Content.Application.Interfaces.Repositories;
using ToeicSpace.Content.Application.Mappings;
using ToeicSpace.Content.Domain.Entities;
using ToeicSpace.Content.Domain.Exceptions;

namespace ToeicSpace.Content.Application.Services;

/// <summary>Shared steps of the learner news endpoints: published article, the learner's reading row, its state.</summary>
public sealed class NewsReadingService(INewsArticleRepository articles, INewsReadingRepository readings)
{
    public async Task<NewsArticle> GetArticleAsync(string slug, CancellationToken ct) =>
        await articles.GetPublishedBySlugAsync(slug, ct) ?? throw NewsErrors.ArticleNotFound();

    /// <summary>The learner's tracked reading of the article; a new one (opened now) is added when missing.</summary>
    public async Task<NewsReading> GetOrOpenAsync(Guid learnerId, NewsArticle article, DateTime now,
        CancellationToken ct)
    {
        var reading = await readings.GetForUpdateAsync(learnerId, article.Id, ct);
        if (reading is not null) return reading;
        reading = NewsReading.Open(learnerId, article.Id, now);
        readings.Add(reading);
        return reading;
    }

    /// <summary>State of the reading with the learner's most recently written reader settings.</summary>
    public async Task<NewsReadingStateDto> PresentAsync(NewsReading reading, NewsArticle article, CancellationToken ct)
    {
        var latest = await readings.GetLatestSettingsAsync(reading.LearnerId, ct);
        var own = reading.Settings is not null
            && (latest.UpdatedAt is null || reading.SettingsUpdatedAt >= latest.UpdatedAt);
        return own
            ? reading.ToStateDto(article, reading.Settings, reading.SettingsUpdatedAt)
            : reading.ToStateDto(article, latest.Settings, latest.UpdatedAt);
    }
}
