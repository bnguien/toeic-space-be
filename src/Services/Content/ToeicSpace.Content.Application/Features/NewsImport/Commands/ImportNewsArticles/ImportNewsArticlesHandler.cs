using System.Text.Json;
using Microsoft.Extensions.Logging;
using ToeicSpace.Content.Application.DTOs.News;
using ToeicSpace.Content.Application.Interfaces;
using ToeicSpace.Content.Application.Interfaces.Repositories;
using ToeicSpace.Content.Application.Interfaces.Services;
using ToeicSpace.Content.Application.Services;
using ToeicSpace.Content.Domain.Entities;
using ToeicSpace.Content.Domain.Exceptions;

namespace ToeicSpace.Content.Application.Features.NewsImport.Commands.ImportNewsArticles;

/// <summary>
/// Validates every file before writing anything, stores the covers, then upserts the articles in one transaction.
/// Nothing is deleted: an article missing from the files stays as it is (hide it by importing it with status hidden).
/// </summary>
public sealed class ImportNewsArticlesHandler(INewsArticleRepository repository, INewsCoverStore covers,
    IUnitOfWork unitOfWork, TimeProvider clock, ILogger<ImportNewsArticlesHandler> logger)
    : IRequestHandler<ImportNewsArticlesCommand, NewsImportResultDto>
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public async Task<NewsImportResultDto> Handle(ImportNewsArticlesCommand request, CancellationToken ct)
    {
        var coverFiles = request.Files.Where(NewsImportParser.IsCover).ToList();
        foreach (var file in coverFiles)
            if (!NewsImportParser.IsCoverName(file.FileName) || !NewsImportParser.IsWebp(file.Content))
                throw NewsErrors.InvalidImportFile(file.FileName, "covers must be WebP images named <hash>.webp.");

        var incoming = new Dictionary<string, NewsArticle>();
        foreach (var file in request.Files.Where(NewsImportParser.IsDocument))
            foreach (var item in NewsImportParser.ParseDocument(file))
            {
                var article = NewsImportParser.Normalize(item, file.FileName);
                if (!incoming.TryAdd(article.Slug, article))
                    throw NewsErrors.InvalidImportFile(file.FileName, $"article '{article.Slug}' appears twice.");
            }

        int coversStored = 0, coversUnchanged = 0;
        foreach (var file in coverFiles)
        {
            if (await covers.SaveAsync(file.FileName, file.Content, ct)) coversStored++;
            else coversUnchanged++;
        }

        int created = 0, updated = 0, unchanged = 0;
        if (incoming.Count > 0)
        {
            var now = clock.GetUtcNow().UtcDateTime;
            await using var transaction = await unitOfWork.BeginTransactionAsync(ct);
            var existing = (await repository.GetBySlugsForUpdateAsync(incoming.Keys.ToArray(), ct))
                .ToDictionary(x => x.Slug);
            foreach (var source in incoming.Values)
            {
                if (!existing.TryGetValue(source.Slug, out var article))
                {
                    source.CreatedAt = now;
                    source.UpdatedAt = now;
                    repository.Add(source);
                    created++;
                }
                else if (Assign(article, source))
                {
                    article.UpdatedAt = now;
                    updated++;
                }
                else unchanged++;
            }
            await unitOfWork.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
        }

        var missing = incoming.Values.Where(x => x.CoverFile is not null && !covers.Exists(x.CoverFile))
            .Select(x => x.Slug).Order().ToList();
        logger.LogInformation(
            "Imported news: articles +{Created}/~{Updated}/={Unchanged}, covers +{CoversStored}/={CoversUnchanged}, missing covers {Missing}",
            created, updated, unchanged, coversStored, coversUnchanged, missing.Count);
        return new NewsImportResultDto(created, updated, unchanged, coversStored, coversUnchanged, missing);
    }

    /// <summary>Copies the imported values; true when anything changed.</summary>
    private static bool Assign(NewsArticle target, NewsArticle source)
    {
        var changed = Set(target.SourceName, source.SourceName, v => target.SourceName = v)
            | Set(target.SourceUrl, source.SourceUrl, v => target.SourceUrl = v)
            | Set(target.SourceId, source.SourceId, v => target.SourceId = v)
            | Set(target.SourceSeries, source.SourceSeries, v => target.SourceSeries = v)
            | Set(target.SourceLevel, source.SourceLevel, v => target.SourceLevel = v)
            | Set(target.Author, source.Author, v => target.Author = v)
            | Set(target.PublishedAt, source.PublishedAt, v => target.PublishedAt = v)
            | Set(target.License, source.License, v => target.License = v)
            | Set(target.Attribution, source.Attribution, v => target.Attribution = v)
            | Set(target.TitleEn, source.TitleEn, v => target.TitleEn = v)
            | Set(target.TitleVi, source.TitleVi, v => target.TitleVi = v)
            | Set(target.SummaryVi, source.SummaryVi, v => target.SummaryVi = v)
            | Set(target.WordCount, source.WordCount, v => target.WordCount = v)
            | Set(target.Minutes, source.Minutes, v => target.Minutes = v)
            | Set(target.Stage, source.Stage, v => target.Stage = v)
            | Set(target.Topic, source.Topic, v => target.Topic = v)
            | Set(target.CoverFile, source.CoverFile, v => target.CoverFile = v)
            | Set(target.CoverAlt, source.CoverAlt, v => target.CoverAlt = v)
            | Set(target.CoverCredit, source.CoverCredit, v => target.CoverCredit = v)
            | Set(target.CoverLicense, source.CoverLicense, v => target.CoverLicense = v)
            | Set(target.CoverSourceUrl, source.CoverSourceUrl, v => target.CoverSourceUrl = v)
            | Set(target.AudioUrl, source.AudioUrl, v => target.AudioUrl = v)
            | Set(target.Status, source.Status, v => target.Status = v)
            | Set(target.Featured, source.Featured, v => target.Featured = v);
        if (JsonSerializer.Serialize(target.Paragraphs, Json) != JsonSerializer.Serialize(source.Paragraphs, Json))
        {
            target.Paragraphs = source.Paragraphs;
            changed = true;
        }
        if (JsonSerializer.Serialize(target.Vocabulary, Json) != JsonSerializer.Serialize(source.Vocabulary, Json))
        {
            target.Vocabulary = source.Vocabulary;
            changed = true;
        }
        return changed;
    }

    private static bool Set<T>(T current, T value, Action<T> assign)
    {
        if (EqualityComparer<T>.Default.Equals(current, value)) return false;
        assign(value);
        return true;
    }
}
