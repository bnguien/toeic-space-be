using System.Data;
using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;
using ToeicSpace.Assessment.Application.Analytics.Common;
using ToeicSpace.Assessment.Application.Analytics.Queries.GetAnalyticsEvents;
using ToeicSpace.Assessment.Application.Analytics.Queries.QueryAnalytics;
using ToeicSpace.Assessment.Infrastructure.Persistence;

namespace ToeicSpace.Assessment.Application.UnitTests.Analytics;

/// <summary>
/// Runs every metric and both event types against the MySQL provider without a server: the connection is
/// never opened and every command returns no rows. A LINQ shape MySQL cannot translate fails here instead
/// of at runtime.
/// </summary>
public class AnalyticsMySqlTranslationTests
{
    private readonly Xunit.Abstractions.ITestOutputHelper _output;

    public AnalyticsMySqlTranslationTests(Xunit.Abstractions.ITestOutputHelper output) => _output = output;

    public static TheoryData<string> MetricNames()
    {
        var data = new TheoryData<string>();
        foreach (var metric in AnalyticsCatalog.Metrics)
        {
            data.Add(metric.Name);
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(MetricNames))]
    public async Task EveryMetric_TranslatesToMySql(string name)
    {
        var metric = AnalyticsCatalog.Find(name)!;
        var commands = new List<string>();
        await using var context = MySqlContext(commands);

        var by = string.Join(',', metric.Dimensions
            .Where(dimension => dimension != metric.RequiredDimension)
            .Take(metric.RequiredDimension is null ? 2 : 1));

        string? Filter(string filter, string value) => metric.Filters.Contains(filter) ? value : null;

        var query = new QueryAnalyticsQuery(
            name,
            by,
            Filter(AnalyticsFilters.From, "2026-08-01"),
            Filter(AnalyticsFilters.To, "2026-09-14"),
            Filter(AnalyticsFilters.TestId, Guid.NewGuid().ToString()),
            Filter(AnalyticsFilters.Part, "5"),
            Filter(AnalyticsFilters.Section, "Reading"),
            Filter(AnalyticsFilters.Difficulty, "Hard"),
            Filter(AnalyticsFilters.Status, metric.Source == AnalyticsSource.Bank ? "Active" : "Completed"),
            Filter(AnalyticsFilters.Mode, "FullTest"));

        var result = await new QueryAnalyticsHandler(context, new FixedTimeProvider()).Handle(query, CancellationToken.None);

        result.Metric.Should().Be(name);
        commands.Should().NotBeEmpty();
        commands.ForEach(_output.WriteLine);
    }

    [Theory]
    [InlineData("attempt")]
    [InlineData("answer")]
    public async Task EventPages_TranslateToMySql(string type)
    {
        var commands = new List<string>();
        await using var context = MySqlContext(commands);
        var cursor = new AnalyticsCursor(type, new DateTime(2026, 9, 1, 8, 0, 0, DateTimeKind.Utc), Guid.NewGuid()).Encode();

        var result = await new GetAnalyticsEventsHandler(context, AnalyticsSeed.Pseudonymizer(), new FixedTimeProvider())
            .Handle(new GetAnalyticsEventsQuery(type, Cursor: cursor, Limit: 50), CancellationToken.None);

        result.Rows.Should().BeEmpty();
        commands.ForEach(_output.WriteLine);
        commands.Should().ContainSingle().Which.Should().Contain("ORDER BY").And.Contain("LIMIT");
    }

    [Fact]
    public async Task RetentionFollowUpDays_TranslateToMySql()
    {
        await using var context = MySqlContext([]);

        var sql = AnalyticsSources
            .ActiveDays(context, new DateTime(2026, 8, 1), new DateTime(2026, 9, 22))
            .ToQueryString();

        sql.Should().Contain("DISTINCT").And.Contain("CONVERT");
    }

    private static AssessmentDbContext MySqlContext(List<string> commands)
        => new(new DbContextOptionsBuilder<AssessmentDbContext>()
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
