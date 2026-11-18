using System.Data;
using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using ToeicSpace.Content.Application.Analytics.Common;
using ToeicSpace.Content.Application.Analytics.Queries.QueryAnalytics;
using ToeicSpace.Content.Infrastructure.Persistence;
using ToeicSpace.Content.Infrastructure.Persistence.Repositories;
using ToeicSpace.Content.Tests.Vocabulary;
using Xunit;

namespace ToeicSpace.Content.Tests.Analytics;

/// <summary>
/// Runs every metric, with every dimension, against the MySQL provider without a server: the connection is never
/// opened and every command returns no rows. A LINQ shape MySQL cannot translate fails here instead of at runtime.
/// </summary>
public sealed class AnalyticsMySqlTranslationTests
{
    public static TheoryData<string, string?> MetricDimensions()
    {
        var data = new TheoryData<string, string?>();

        foreach (var metric in AnalyticsCatalog.Metrics)
        {
            data.Add(metric.Name, null);

            foreach (var dimension in metric.Dimensions.Where(dimension => dimension != metric.RequiredDimension))
            {
                data.Add(metric.Name, dimension);
            }
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(MetricDimensions))]
    public async Task EveryMetricAndDimension_TranslatesToMySql(string name, string? by)
    {
        var metric = AnalyticsCatalog.Find(name)!;
        var commands = new List<string>();
        await using var context = MySqlContext(commands);

        string? Filter(string filter, string value) => metric.Filters.Contains(filter) ? value : null;

        var query = new QueryAnalyticsQuery(
            name,
            by,
            Filter(AnalyticsFilters.From, "2026-09-01"),
            Filter(AnalyticsFilters.To, "2026-11-14"),
            Filter(AnalyticsFilters.Status, metric.Source == AnalyticsSource.Lessons ? "Published" : "Archived"),
            Filter(AnalyticsFilters.Level, "Beginner"),
            Filter(AnalyticsFilters.CourseId, metric.Source == AnalyticsSource.Lessons ? Guid.NewGuid().ToString() : "7"));

        var clock = new FixedTimeProvider(new DateTimeOffset(2026, 11, 14, 12, 0, 0, TimeSpan.Zero));
        var result = await new QueryAnalyticsHandler(new ContentAnalyticsRepository(context), clock)
            .Handle(query, CancellationToken.None);

        Assert.Equal(name, result.Metric);
        Assert.NotEmpty(commands);
        // Request values reach MySQL as typed parameters, never as text inside the SQL.
        Assert.All(commands, command => Assert.DoesNotContain("2026-09-01", command));
    }

    private static ContentDbContext MySqlContext(List<string> commands)
        => new(new DbContextOptionsBuilder<ContentDbContext>()
            .UseMySql("Server=127.0.0.1;Port=1;Database=translation_only", new MySqlServerVersion(new Version(8, 4, 0)))
            .AddInterceptors(new OfflineInterceptor(commands))
            .Options);

    /// <summary>Skips opening the connection and answers every query with an empty result.</summary>
    private sealed class OfflineInterceptor(List<string> commands) : DbCommandInterceptor, IDbConnectionInterceptor
    {
        public ValueTask<InterceptionResult> ConnectionOpeningAsync(
            DbConnection connection,
            ConnectionEventData eventData,
            InterceptionResult result,
            CancellationToken cancellationToken = default)
            => ValueTask.FromResult(InterceptionResult.Suppress());

        public InterceptionResult ConnectionOpening(
            DbConnection connection,
            ConnectionEventData eventData,
            InterceptionResult result)
            => InterceptionResult.Suppress();

        public ValueTask<InterceptionResult> ConnectionClosingAsync(
            DbConnection connection,
            ConnectionEventData eventData,
            InterceptionResult result)
            => ValueTask.FromResult(InterceptionResult.Suppress());

        public InterceptionResult ConnectionClosing(
            DbConnection connection,
            ConnectionEventData eventData,
            InterceptionResult result)
            => InterceptionResult.Suppress();

        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<DbDataReader> result,
            CancellationToken cancellationToken = default)
        {
            commands.Add(command.CommandText);
            return ValueTask.FromResult(InterceptionResult<DbDataReader>.SuppressWithResult(new DataTable().CreateDataReader()));
        }
    }
}
