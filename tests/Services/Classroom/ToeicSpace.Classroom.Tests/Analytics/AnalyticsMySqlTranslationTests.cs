using System.Data;
using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using ToeicSpace.Classroom.Application.Analytics.Common;
using ToeicSpace.Classroom.Application.Analytics.Queries.QueryAnalytics;
using ToeicSpace.Classroom.Infrastructure.Persistence;
using ToeicSpace.Classroom.Infrastructure.Persistence.Repositories;
using Xunit;
using Xunit.Abstractions;

namespace ToeicSpace.Classroom.Tests.Analytics;

/// <summary>
/// Runs every metric against the MySQL provider without a server: the connection is never opened and every
/// command returns no rows. A LINQ shape MySQL cannot translate fails here instead of at runtime.
/// </summary>
public sealed class AnalyticsMySqlTranslationTests
{
    private readonly ITestOutputHelper _output;

    public AnalyticsMySqlTranslationTests(ITestOutputHelper output)
    {
        _output = output;
    }

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
            Filter(AnalyticsFilters.Status, "Active"),
            Filter(AnalyticsFilters.ClassId, Guid.NewGuid().ToString()));

        var result = await new QueryAnalyticsHandler(new ClassroomAnalyticsReader(context), new AnalyticsClock())
            .Handle(query, CancellationToken.None);

        Assert.Equal(name, result.Metric);
        Assert.NotEmpty(commands);
        Assert.All(commands, command => Assert.Contains("LIMIT", command));
        Assert.All(commands, command => Assert.DoesNotContain("StudentReadModels", command));
        Assert.All(commands, command => Assert.DoesNotContain("TeacherReadModels", command));
        commands.ForEach(_output.WriteLine);
    }

    [Fact]
    public async Task ClassLabels_TranslateToMySql()
    {
        await using var context = MySqlContext([]);
        var reader = new ClassroomAnalyticsReader(context);

        var codes = await reader.GetClassCodesAsync([Guid.NewGuid(), Guid.NewGuid()], CancellationToken.None);

        Assert.Empty(codes);
    }

    private static ClassroomDbContext MySqlContext(List<string> commands)
        => new(new DbContextOptionsBuilder<ClassroomDbContext>()
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
