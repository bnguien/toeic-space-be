using System.Text.Json;
using System.Text.Json.Serialization;
using ToeicSpace.Assessment.Application.Analytics.Queries.QueryAnalytics;

namespace ToeicSpace.Assessment.Application.UnitTests.Analytics;

/// <summary>Whitelisted metrics, small-group suppression and the shape of the rows.</summary>
public class AnalyticsQueryTests
{
    [Theory]
    [InlineData("attempt_count", 5.0)]
    [InlineData("accuracy", 1.0)]
    public async Task TestGroupingAndFiltering_PreservePracticeAttemptsWithoutATest(string metric, double expectedValue)
    {
        using var database = new TestDatabase();
        var test = database.AddTest(ContentStatus.Active);
        var practiceSet = database.AddPracticeSet(ToeicPart.Part5);
        var question = database.AddQuestion(test.Id, ToeicPart.Part5, 101);

        foreach (var learner in AnalyticsSeed.Learners(5))
        {
            database.AddAttempt(learner, test.Id, answers: [(question, true)]);
            var attempt = database.AddAttempt(learner, null, mode: AttemptMode.Practice, answers: [(question, true)]);
            attempt.PracticeSetId = practiceSet.Id;
        }

        database.Context.SaveChanges();

        var grouped = await database.QueryAsync(new QueryAnalyticsQuery(metric, By: "test"));

        grouped.Rows.Should().HaveCount(2);
        grouped.Suppressed.Should().Be(0);
        grouped.Rows.Single(row => row["test"] is null)["value"].Should().Be(expectedValue);
        grouped.Rows.Single(row => (string?)row["test"] == test.Id.ToString())["value"].Should().Be(expectedValue);
        grouped.Labels.Should().ContainSingle().Which.Key.Should().Be(test.Id.ToString());

        var filtered = await database.QueryAsync(new QueryAnalyticsQuery(metric, By: "test", TestId: test.Id.ToString()));

        filtered.Rows.Should().ContainSingle();
        filtered.Cell(0, "test").Should().Be(test.Id.ToString());
        filtered.Cell(0, "value").Should().Be(expectedValue);

        var practice = await database.QueryAsync(new QueryAnalyticsQuery(metric, By: "test", Mode: "Practice"));

        practice.Rows.Should().ContainSingle();
        practice.Cell(0, "test").Should().BeNull();
        practice.Cell(0, "value").Should().Be(expectedValue);
    }

    [Fact]
    public async Task GroupsWithFewerThanFiveLearners_AreSuppressed()
    {
        using var database = new TestDatabase();
        var popular = database.AddTest(ContentStatus.Active, "ETS-2026-TEST-01");
        var quiet = database.AddTest(ContentStatus.Active, "ETS-2026-TEST-02");

        foreach (var learner in AnalyticsSeed.Learners(5))
        {
            database.AddAttempt(learner, popular.Id);
            database.AddAttempt(learner, popular.Id);
        }

        foreach (var learner in AnalyticsSeed.Learners(4))
        {
            database.AddAttempt(learner, quiet.Id);
        }

        var result = await database.QueryAsync(new QueryAnalyticsQuery("attempt_count", By: "test"));

        result.Suppressed.Should().Be(1);
        result.Rows.Should().HaveCount(2);

        var shown = result.Rows.Single(row => (string?)row["test"] == popular.Id.ToString());
        shown["value"].Should().Be(10.0);
        shown["n"].Should().Be(10);

        var hidden = result.Rows.Single(row => (string?)row["test"] == quiet.Id.ToString());
        hidden["value"].Should().BeNull();
        hidden["n"].Should().Be(0);

        result.Labels.Should().ContainKey(popular.Id.ToString()).WhoseValue.Should().Be("ETS-2026-TEST-01");
    }

    [Fact]
    public async Task WithoutDimensions_ReturnsOneRow()
    {
        using var database = new TestDatabase();
        var test = database.AddTest(ContentStatus.Active);

        foreach (var learner in AnalyticsSeed.Learners(6))
        {
            database.AddAttempt(learner, test.Id);
        }

        var result = await database.QueryAsync(new QueryAnalyticsQuery("active_learners"));

        result.By.Should().BeEmpty();
        result.Rows.Should().ContainSingle();
        result.Cell(0, "value").Should().Be(6.0);
        result.From.Should().Be(new DateOnly(2026, 6, 17));
        result.To.Should().Be(new DateOnly(2026, 9, 14));
    }

    [Fact]
    public async Task DateRange_ExcludesAttemptsOutsideIt()
    {
        using var database = new TestDatabase();
        var test = database.AddTest(ContentStatus.Active);

        foreach (var learner in AnalyticsSeed.Learners(5))
        {
            database.AddAttempt(learner, test.Id, start: new DateTime(2026, 9, 2, 23, 59, 0, DateTimeKind.Utc));
            database.AddAttempt(learner, test.Id, start: new DateTime(2026, 9, 3, 0, 0, 0, DateTimeKind.Utc));
        }

        var result = await database.QueryAsync(new QueryAnalyticsQuery("attempt_count", From: "2026-09-01", To: "2026-09-02"));

        result.Cell(0, "value").Should().Be(5.0);
    }

    [Fact]
    public async Task Accuracy_ByPartAndSection()
    {
        using var database = new TestDatabase();
        var test = database.AddTest(ContentStatus.Active);
        var photo = database.AddQuestion(test.Id, ToeicPart.Part1, 1);
        var grammar = database.AddQuestion(test.Id, ToeicPart.Part5, 101);

        foreach (var learner in AnalyticsSeed.Learners(5))
        {
            database.AddAttempt(learner, test.Id, answers: [(photo, false), (grammar, true)]);
        }

        var byPart = await database.QueryAsync(new QueryAnalyticsQuery("accuracy", By: "part"));

        byPart.Rows.Select(row => row["part"]).Should().Equal("Part1", "Part5");
        byPart.Cell(0, "value").Should().Be(0.0);
        byPart.Cell(1, "value").Should().Be(1.0);
        byPart.Cell(1, "n").Should().Be(5);

        var bySection = await database.QueryAsync(new QueryAnalyticsQuery("accuracy", By: "section", Section: "reading"));

        bySection.Rows.Should().ContainSingle();
        bySection.Cell(0, "section").Should().Be("Reading");
        bySection.Cell(0, "value").Should().Be(1.0);
    }

    [Fact]
    public async Task QuestionPValue_ListsTheHardestQuestionFirst()
    {
        using var database = new TestDatabase();
        var test = database.AddTest(ContentStatus.Active);
        var easy = database.AddQuestion(test.Id, ToeicPart.Part5, 101);
        var hard = database.AddQuestion(test.Id, ToeicPart.Part5, 102);

        foreach (var learner in AnalyticsSeed.Learners(5))
        {
            database.AddAttempt(learner, test.Id, answers: [(easy, true), (hard, false)]);
        }

        var result = await database.QueryAsync(new QueryAnalyticsQuery("question_p_value", By: "part"));

        result.By.Should().Equal("question", "part");
        result.Cell(0, "question").Should().Be(hard.Id.ToString());
        result.Cell(0, "value").Should().Be(0.0);
        result.Cell(1, "question").Should().Be(easy.Id.ToString());
        result.Cell(1, "part").Should().Be("Part5");
    }

    [Fact]
    public async Task ScoreMetrics_UseCompletedAttemptsOnly()
    {
        using var database = new TestDatabase();
        var test = database.AddTest(ContentStatus.Active);
        var scores = new[] { 300, 400, 500, 600, 900 };
        var learners = AnalyticsSeed.Learners(5);

        for (var index = 0; index < scores.Length; index++)
        {
            database.AddAttempt(learners[index], test.Id, score: scores[index]);
        }

        database.AddAttempt(learners[0], test.Id, status: AttemptStatus.Abandoned, score: 990);

        (await database.QueryAsync(new QueryAnalyticsQuery("avg_score"))).Cell(0, "value").Should().Be(540.0);
        (await database.QueryAsync(new QueryAnalyticsQuery("median_score"))).Cell(0, "value").Should().Be(500.0);
        (await database.QueryAsync(new QueryAnalyticsQuery("completion_rate"))).Cell(0, "value").Should().Be(0.8333);
    }

    [Fact]
    public async Task ScoreDistribution_AlwaysGroupsByScoreBand()
    {
        using var database = new TestDatabase();
        var test = database.AddTest(ContentStatus.Active);

        foreach (var learner in AnalyticsSeed.Learners(5))
        {
            database.AddAttempt(learner, test.Id, score: 550);
        }

        database.AddAttempt(Guid.NewGuid(), test.Id, score: 990);

        var result = await database.QueryAsync(new QueryAnalyticsQuery("score_distribution"));

        result.By.Should().Equal("score_band");
        result.Rows.Select(row => row["score_band"]).Should().Equal("500-599", "900-990");
        result.Cell(0, "value").Should().Be(5.0);
        result.Cell(1, "value").Should().BeNull();
        result.Suppressed.Should().Be(1);
    }

    [Fact]
    public async Task ScoreGain_ComparesTheLatestWithTheFirstAttemptOfEachLearner()
    {
        using var database = new TestDatabase();
        var test = database.AddTest(ContentStatus.Active);

        foreach (var learner in AnalyticsSeed.Learners(5))
        {
            database.AddAttempt(learner, test.Id, start: AnalyticsSeed.Recent.AddDays(-5), score: 400);
            database.AddAttempt(learner, test.Id, start: AnalyticsSeed.Recent, score: 550);
        }

        var result = await database.QueryAsync(new QueryAnalyticsQuery("score_gain"));

        result.Cell(0, "value").Should().Be(150.0);
        result.Cell(0, "n").Should().Be(5);
    }

    [Fact]
    public async Task Retention_CountsLearnersWhoReturnWithinSevenDays()
    {
        using var database = new TestDatabase();
        var test = database.AddTest(ContentStatus.Active);
        var firstDay = new DateTime(2026, 9, 1, 8, 0, 0, DateTimeKind.Utc);
        var learners = AnalyticsSeed.Learners(5);

        foreach (var learner in learners)
        {
            database.AddAttempt(learner, test.Id, start: firstDay);
        }

        foreach (var learner in learners.Take(3))
        {
            database.AddAttempt(learner, test.Id, start: firstDay.AddDays(3));
        }

        // Too recent: the 7-day follow-up window has not passed yet (today is 2026-09-14).
        database.AddAttempt(Guid.NewGuid(), test.Id, start: new DateTime(2026, 9, 10, 8, 0, 0, DateTimeKind.Utc));

        var result = await database.QueryAsync(new QueryAnalyticsQuery("retention_7d"));

        result.Cell(0, "value").Should().Be(0.6);
        result.Cell(0, "n").Should().Be(5);

        var newLearners = await database.QueryAsync(new QueryAnalyticsQuery("new_learners", By: "week"));

        newLearners.Rows.Select(row => row["week"]).Should().Equal("2026-08-31", "2026-09-07");
        newLearners.Cell(0, "value").Should().Be(5.0);
        newLearners.Cell(1, "value").Should().BeNull();
    }

    [Fact]
    public async Task BankMetrics_AreNotSuppressed()
    {
        using var database = new TestDatabase();
        database.AddQuestion(null, ToeicPart.Part5, null);
        database.AddQuestion(null, ToeicPart.Part6, null, difficulty: QuestionDifficulty.Hard);

        var result = await database.QueryAsync(new QueryAnalyticsQuery("question_count", By: "part"));

        result.Suppressed.Should().Be(0);
        result.Rows.Select(row => row["part"]).Should().Equal("Part5", "Part6");
        result.Rows.Select(row => row["value"]).Should().Equal(1.0, 1.0);

        var hard = await database.QueryAsync(new QueryAnalyticsQuery("question_count", Difficulty: "4"));
        hard.Cell(0, "value").Should().Be(1.0);
    }

    [Fact]
    public async Task Limit_TruncatesTheRows()
    {
        using var database = new TestDatabase();

        foreach (var part in Enum.GetValues<ToeicPart>())
        {
            database.AddQuestion(null, part, null);
        }

        var result = await database.QueryAsync(new QueryAnalyticsQuery("question_count", By: "part", Limit: 3));

        result.Rows.Should().HaveCount(3);
        result.Truncated.Should().BeTrue();
    }

    [Fact]
    public async Task Json_KeepsNullValuesAndNeverContainsUserIds()
    {
        using var database = new TestDatabase();
        var test = database.AddTest(ContentStatus.Active);
        var learners = AnalyticsSeed.Learners(3);

        foreach (var learner in learners)
        {
            database.AddAttempt(learner, test.Id);
        }

        var result = await database.QueryAsync(new QueryAnalyticsQuery("attempt_count", By: "day"));

        // Same settings as the API: enums as strings, null properties omitted.
        var json = JsonSerializer.Serialize(result, new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            Converters = { new JsonStringEnumConverter() }
        });

        json.Should().Contain("\"value\":null");
        json.Should().Contain("\"day\":\"2026-09-10\"");
        foreach (var learner in learners)
        {
            json.Should().NotContain(learner.ToString());
        }
    }

    [Fact]
    public async Task InvalidQuery_IsRejectedByTheHandlerToo()
    {
        using var database = new TestDatabase();

        var act = () => database.QueryAsync(new QueryAnalyticsQuery("password_hash"));

        (await act.Should().ThrowAsync<AppException>())
            .Which.Type.Should().Be(ErrorType.Validation);
    }
}
