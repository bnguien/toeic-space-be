using System.Text.Json;
using System.Text.Json.Serialization;
using ToeicSpace.Classroom.Application.Analytics.Common;
using ToeicSpace.Classroom.Application.Analytics.Queries.QueryAnalytics;
using ToeicSpace.Classroom.Domain.Enums;
using ToeicSpace.Classroom.Domain.Exceptions;
using Xunit;

namespace ToeicSpace.Classroom.Tests.Analytics;

/// <summary>Metric computations, small-group suppression and the shape of the rows.</summary>
public sealed class AnalyticsQueryTests
{
    private static readonly DateOnly September = new(2026, 9, 1);

    [Fact]
    public async Task ClassCount_UsesCreatedAtAndSkipsDeletedClasses()
    {
        using var database = new AnalyticsDatabase();
        database.AddClass("A-1", ClassStatus.Active, createdAt: new DateTime(2026, 8, 5, 0, 0, 0, DateTimeKind.Utc));
        database.AddClass("A-2", ClassStatus.Draft, createdAt: new DateTime(2026, 9, 2, 0, 0, 0, DateTimeKind.Utc));
        database.AddClass("A-3", ClassStatus.Draft, createdAt: new DateTime(2026, 9, 3, 0, 0, 0, DateTimeKind.Utc));
        database.AddClass("D-1", ClassStatus.Deleted);
        database.AddClass("OLD", ClassStatus.Active, createdAt: new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc));

        var total = await database.QueryAsync(new QueryAnalyticsQuery("class_count"));
        Assert.Equal(3.0, total.Cell(0, "value"));
        Assert.Equal(new DateOnly(2026, 6, 17), total.From);

        var byMonth = await database.QueryAsync(new QueryAnalyticsQuery("class_count", By: "month,status"));
        Assert.Equal(["2026-08", "2026-09"], byMonth.Rows.Select(row => row["month"]));
        Assert.Equal(["Active", "Draft"], byMonth.Rows.Select(row => row["status"]));
        Assert.Equal([1.0, 2.0], byMonth.Rows.Select(row => row["value"]));
        Assert.Equal(0, byMonth.Suppressed);

        var drafts = await database.QueryAsync(new QueryAnalyticsQuery("class_count", Status: "draft"));
        Assert.Equal(2.0, drafts.Cell(0, "value"));
    }

    [Fact]
    public async Task ActiveClasses_CountsOpenClassesByStartMonthWhateverTheRange()
    {
        using var database = new AnalyticsDatabase();
        database.AddClass("A-1", ClassStatus.Active, createdAt: new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            startDate: new DateOnly(2026, 8, 20));
        database.AddClass("A-2", ClassStatus.Draft, startDate: new DateOnly(2026, 10, 1));
        database.AddClass("A-3", ClassStatus.Draft);
        database.AddClass("C-1", ClassStatus.Completed, startDate: new DateOnly(2026, 8, 1));
        database.AddClass("R-1", ClassStatus.Archived);

        var result = await database.QueryAsync(new QueryAnalyticsQuery("active_classes", By: "month"));

        Assert.Null(result.From);
        Assert.Equal(["2026-08", "2026-10", null], result.Rows.Select(row => row["month"]));
        Assert.Equal([1.0, 1.0, 1.0], result.Rows.Select(row => row["value"]));

        var byStatus = await database.QueryAsync(new QueryAnalyticsQuery("active_classes", By: "status"));
        Assert.Equal(["Draft", "Active"], byStatus.Rows.Select(row => row["status"]));
        Assert.Equal([2.0, 1.0], byStatus.Rows.Select(row => row["value"]));
    }

    [Fact]
    public async Task ClassEnrollment_ByClassCarriesClassCodesAsLabels()
    {
        using var database = new AnalyticsDatabase();
        var big = database.AddClass("TOEIC-650");
        var small = database.AddClass("TOEIC-450");
        var deleted = database.AddClass("GONE", ClassStatus.Deleted);
        database.Enroll(big.Id, AnalyticsDatabase.Learners(3));
        database.Enroll(big.Id, AnalyticsDatabase.Learners(2), withdrawn: true);
        database.Enroll(small.Id, AnalyticsDatabase.Learners(1));
        database.Enroll(deleted.Id, AnalyticsDatabase.Learners(4));
        database.Enroll(small.Id, AnalyticsDatabase.Learners(1), enrolledAt: new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));

        var result = await database.QueryAsync(new QueryAnalyticsQuery("class_enrollment", By: "class"));

        // Not a learner metric: a class with one enrollment is still shown.
        Assert.Equal(0, result.Suppressed);
        Assert.Equal(5.0, result.RowWhere("class", big.Id.ToString())["value"]);
        Assert.Equal(1.0, result.RowWhere("class", small.Id.ToString())["value"]);
        Assert.Equal(2, result.Rows.Count);
        Assert.Equal("TOEIC-650", result.Labels![big.Id.ToString()]);
        Assert.Equal("TOEIC-450", result.Labels[small.Id.ToString()]);

        var withdrawn = await database.QueryAsync(new QueryAnalyticsQuery("class_enrollment", Status: "Withdrawn"));
        Assert.Equal(2.0, withdrawn.Cell(0, "value"));

        var oneClass = await database.QueryAsync(new QueryAnalyticsQuery("class_enrollment", By: "status", ClassId: big.Id.ToString()));
        Assert.Equal(["Active", "Withdrawn"], oneClass.Rows.Select(row => row["status"]));
        Assert.Equal([3.0, 2.0], oneClass.Rows.Select(row => row["value"]));
    }

    [Fact]
    public async Task EnrollmentStatusMix_AlwaysGroupsByStatusAndTreatsWithdrawnAtAsWithdrawn()
    {
        using var database = new AnalyticsDatabase();
        var classroom = database.AddClass("MIX");
        database.Enroll(classroom.Id, AnalyticsDatabase.Learners(2), enrolledAt: new DateTime(2024, 5, 1, 0, 0, 0, DateTimeKind.Utc));
        database.Enroll(classroom.Id, AnalyticsDatabase.Learners(1), withdrawn: true);
        database.Context.ClassEnrollments.Add(new()
        {
            ClassId = classroom.Id,
            StudentId = Guid.NewGuid(),
            Status = EnrollmentStatus.Active,
            EnrolledAt = AnalyticsDatabase.Recent,
            WithdrawnAt = AnalyticsDatabase.Recent
        });
        await database.Context.SaveChangesAsync();

        var result = await database.QueryAsync(new QueryAnalyticsQuery("enrollment_status_mix"));

        Assert.Equal(["status"], result.By);
        Assert.Null(result.From);
        Assert.Equal(["Active", "Withdrawn"], result.Rows.Select(row => row["status"]));
        Assert.Equal([2.0, 2.0], result.Rows.Select(row => row["value"]));
    }

    [Fact]
    public async Task EnrolledLearners_CountsDistinctLearnersAndSuppressesSmallGroups()
    {
        using var database = new AnalyticsDatabase();
        var first = database.AddClass("L-1");
        var second = database.AddClass("L-2");
        var learners = AnalyticsDatabase.Learners(5);
        database.Enroll(first.Id, learners, enrolledAt: new DateTime(2026, 8, 3, 0, 0, 0, DateTimeKind.Utc));
        database.Enroll(second.Id, learners.Take(2), enrolledAt: new DateTime(2026, 8, 4, 0, 0, 0, DateTimeKind.Utc));
        database.Enroll(second.Id, AnalyticsDatabase.Learners(3), enrolledAt: new DateTime(2026, 9, 7, 0, 0, 0, DateTimeKind.Utc));

        var total = await database.QueryAsync(new QueryAnalyticsQuery("enrolled_learners"));
        Assert.Equal(8.0, total.Cell(0, "value"));
        Assert.Equal(8, total.Cell(0, "n"));

        var byMonth = await database.QueryAsync(new QueryAnalyticsQuery("enrolled_learners", By: "month"));
        Assert.Equal(["2026-08", "2026-09"], byMonth.Rows.Select(row => row["month"]));
        Assert.Equal(5.0, byMonth.Cell(0, "value"));
        Assert.Null(byMonth.Cell(1, "value"));
        Assert.Equal(0, byMonth.Cell(1, "n"));
        Assert.Equal(1, byMonth.Suppressed);
    }

    [Fact]
    public async Task WithdrawRate_UsesClassesStartedInTheRange()
    {
        using var database = new AnalyticsDatabase();
        var started = database.AddClass("W-1", startDate: September);
        var tiny = database.AddClass("W-2", startDate: September.AddDays(3));
        var later = database.AddClass("W-3", startDate: new DateOnly(2026, 10, 1));
        database.Enroll(started.Id, AnalyticsDatabase.Learners(3));
        database.Enroll(started.Id, AnalyticsDatabase.Learners(2), withdrawn: true);
        database.Enroll(tiny.Id, AnalyticsDatabase.Learners(1), withdrawn: true);
        database.Enroll(later.Id, AnalyticsDatabase.Learners(6), withdrawn: true);

        var total = await database.QueryAsync(new QueryAnalyticsQuery("withdraw_rate"));
        Assert.Equal(0.5, total.Cell(0, "value"));
        Assert.Equal(6, total.Cell(0, "n"));

        var byClass = await database.QueryAsync(new QueryAnalyticsQuery("withdraw_rate", By: "class"));
        Assert.Equal(0.4, byClass.RowWhere("class", started.Id.ToString())["value"]);
        Assert.Null(byClass.RowWhere("class", tiny.Id.ToString())["value"]);
        Assert.Equal(1, byClass.Suppressed);
        Assert.Equal("W-1", byClass.Labels![started.Id.ToString()]);
    }

    [Fact]
    public async Task ClassSizeAvg_CountsEmptyClassesAsZero()
    {
        using var database = new AnalyticsDatabase();
        var full = database.AddClass("S-1", startDate: September);
        database.AddClass("S-2", ClassStatus.Draft, startDate: September.AddDays(10));
        database.AddClass("S-3", startDate: new DateOnly(2026, 1, 5));
        database.Enroll(full.Id, AnalyticsDatabase.Learners(6));
        database.Enroll(full.Id, AnalyticsDatabase.Learners(1), withdrawn: true);

        var result = await database.QueryAsync(new QueryAnalyticsQuery("class_size_avg", By: "month"));

        Assert.Equal(["2026-09"], result.Rows.Select(row => row["month"]));
        Assert.Equal(3.0, result.Cell(0, "value"));
        Assert.Equal(2, result.Cell(0, "n"));
    }

    [Fact]
    public async Task TeacherLoad_RanksTeachersByOpaqueKeyOnly()
    {
        using var database = new AnalyticsDatabase();
        var busy = Guid.NewGuid();
        var light = Guid.NewGuid();
        database.AddClass("T-1", ClassStatus.Active, teacherId: busy);
        database.AddClass("T-2", ClassStatus.Draft, teacherId: busy);
        database.AddClass("T-3", ClassStatus.Completed, teacherId: busy);
        database.AddClass("T-4", ClassStatus.Active, teacherId: light);
        var moved = database.AddClass("T-5", ClassStatus.Draft, teacherId: light);
        database.Unassign(moved.Id);

        var result = await database.QueryAsync(new QueryAnalyticsQuery("teacher_load"));

        Assert.Equal(["teacher"], result.By);
        Assert.Equal("teachers", result.CountUnit);
        Assert.Equal([2.0, 1.0], result.Rows.Select(row => row["value"]));
        Assert.Equal(AnalyticsGrouping.OpaqueId("teacher", busy), result.Cell(0, "teacher"));
        Assert.Matches("^[0-9a-f]{8}$", (string)result.Cell(1, "teacher")!);
        Assert.Null(result.Labels);
    }

    [Fact]
    public async Task WithoutDimensions_ReturnsOneRowEvenWhenEmpty()
    {
        using var database = new AnalyticsDatabase();

        var count = await database.QueryAsync(new QueryAnalyticsQuery("class_enrollment"));
        Assert.Equal(0.0, Assert.Single(count.Rows)["value"]);

        var ratio = await database.QueryAsync(new QueryAnalyticsQuery("withdraw_rate"));
        Assert.Null(Assert.Single(ratio.Rows)["value"]);
        Assert.Equal(1, ratio.Suppressed);
    }

    [Fact]
    public async Task DateRange_IsInclusiveInUtcDays()
    {
        using var database = new AnalyticsDatabase();
        var classroom = database.AddClass("R-1");
        database.Enroll(classroom.Id, AnalyticsDatabase.Learners(2), enrolledAt: new DateTime(2026, 9, 2, 23, 59, 0, DateTimeKind.Utc));
        database.Enroll(classroom.Id, AnalyticsDatabase.Learners(3), enrolledAt: new DateTime(2026, 9, 3, 0, 0, 0, DateTimeKind.Utc));

        var result = await database.QueryAsync(new QueryAnalyticsQuery("class_enrollment", From: "2026-09-01", To: "2026-09-02"));

        Assert.Equal(2.0, result.Cell(0, "value"));
    }

    [Fact]
    public async Task Limit_TruncatesTheRows()
    {
        using var database = new AnalyticsDatabase();
        for (var day = 1; day <= 4; day++)
        {
            database.AddClass($"D-{day}", createdAt: new DateTime(2026, 9, day, 0, 0, 0, DateTimeKind.Utc));
        }

        var result = await database.QueryAsync(new QueryAnalyticsQuery("class_count", By: "day", Limit: 3));

        Assert.Equal(3, result.Rows.Count);
        Assert.True(result.Truncated);
        Assert.Equal("2026-09-01", result.Cell(0, "day"));
    }

    [Fact]
    public async Task Json_KeepsNullValuesAndNeverContainsPeopleIds()
    {
        using var database = new AnalyticsDatabase();
        var teacher = Guid.NewGuid();
        var classroom = database.AddClass("J-1", startDate: September, teacherId: teacher);
        var learners = AnalyticsDatabase.Learners(3);
        database.Enroll(classroom.Id, learners);

        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };
        var json = string.Join('\n',
            JsonSerializer.Serialize(await database.QueryAsync(new QueryAnalyticsQuery("enrolled_learners", By: "day")), options),
            JsonSerializer.Serialize(await database.QueryAsync(new QueryAnalyticsQuery("withdraw_rate", By: "class")), options),
            JsonSerializer.Serialize(await database.QueryAsync(new QueryAnalyticsQuery("teacher_load")), options));

        Assert.Contains("\"value\":null", json);
        Assert.Contains("\"day\":\"2026-09-10\"", json);
        Assert.DoesNotContain(teacher.ToString(), json);
        Assert.DoesNotContain("Class J-1", json);
        Assert.All(learners, learner => Assert.DoesNotContain(learner.ToString(), json));
    }

    [Fact]
    public async Task InvalidQuery_IsRejectedByTheHandlerToo()
    {
        using var database = new AnalyticsDatabase();

        var error = await Assert.ThrowsAsync<ClassRuleException>(() =>
            database.QueryAsync(new QueryAnalyticsQuery("password_hash", By: "nope")));

        Assert.Equal("validation_error", error.Code);
        Assert.Equal(400, error.StatusCode);
        Assert.Contains("metric", error.Errors!.Keys);
    }
}
