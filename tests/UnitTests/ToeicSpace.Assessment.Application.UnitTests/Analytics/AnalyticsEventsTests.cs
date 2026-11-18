using System.Text.Json;
using System.Text.RegularExpressions;
using ToeicSpace.Assessment.Application.Analytics.Queries.GetAnalyticsEvents;

namespace ToeicSpace.Assessment.Application.UnitTests.Analytics;

/// <summary>Anonymised event export: keyed pseudonyms, stable cursor paging, refused without a key.</summary>
public class AnalyticsEventsTests
{
    [Theory]
    [InlineData("attempt")]
    [InlineData("answer")]
    public async Task Events_PreserveNullableTestIds(string type)
    {
        using var database = new TestDatabase();
        var test = database.AddTest(ContentStatus.Active);
        var practiceSet = database.AddPracticeSet(ToeicPart.Part5);
        var question = database.AddQuestion(test.Id, ToeicPart.Part5, 101);
        var learner = Guid.NewGuid();
        var fullTest = database.AddAttempt(learner, test.Id, answers: [(question, true)]);
        var practice = database.AddAttempt(learner, null, mode: AttemptMode.Practice, answers: [(question, false)]);
        practice.PracticeSetId = practiceSet.Id;
        database.Context.SaveChanges();

        var result = await database.EventsAsync(new GetAnalyticsEventsQuery(type));

        result.Rows.Should().HaveCount(2);
        var pseudonymizer = AnalyticsSeed.Pseudonymizer();
        result.Rows.Single(row => row.Attempt == pseudonymizer.Attempt(fullTest.Id)).TestId.Should().Be(test.Id);
        var practiceEvent = result.Rows.Single(row => row.Attempt == pseudonymizer.Attempt(practice.Id));
        practiceEvent.TestId.Should().BeNull();
        practiceEvent.Mode.Should().Be(AttemptMode.Practice);

        var json = JsonSerializer.SerializeToElement(practiceEvent, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        json.GetProperty("testId").ValueKind.Should().Be(JsonValueKind.Null);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("too-short-key")]
    public async Task WithoutPseudonymKey_ReturnsServiceUnavailable(string? key)
    {
        using var database = new TestDatabase();

        var act = () => database.EventsAsync(new GetAnalyticsEventsQuery("attempt"), AnalyticsSeed.Pseudonymizer(key));

        var error = (await act.Should().ThrowAsync<AppException>()).Which;
        error.Type.Should().Be(ErrorType.ServiceUnavailable);
        error.Message.Should().Be("analytics pseudonym key not configured");
    }

    [Fact]
    public async Task AttemptEvents_PageWithoutGapsOrDuplicates()
    {
        using var database = new TestDatabase();
        var test = database.AddTest(ContentStatus.Active);
        var learner = Guid.NewGuid();
        var attempts = new List<ToeicAttempt>();

        // Two attempts share a start time, so the ID breaks the tie.
        for (var index = 0; index < 5; index++)
        {
            attempts.Add(database.AddAttempt(learner, test.Id, start: AnalyticsSeed.Recent.AddMinutes(index == 2 ? 1 : index)));
        }

        var seen = new List<string>();
        string? cursor = null;
        var pages = 0;

        do
        {
            var page = await database.EventsAsync(new GetAnalyticsEventsQuery("attempt", Cursor: cursor, Limit: 2));
            seen.AddRange(page.Rows.Select(row => row.Attempt));
            page.Rows.Should().OnlyContain(row => row.TotalScore == 500 && row.Status == AttemptStatus.Completed);
            cursor = page.NextCursor;
            pages++;
        }
        while (cursor is not null && pages < 10);

        pages.Should().Be(3);
        seen.Should().HaveCount(5).And.OnlyHaveUniqueItems();

        var pseudonymizer = AnalyticsSeed.Pseudonymizer();
        seen.Should().BeEquivalentTo(attempts.Select(attempt => pseudonymizer.Attempt(attempt.Id)));
    }

    [Fact]
    public async Task Pseudonyms_AreStableHexAndHideTheUserId()
    {
        using var database = new TestDatabase();
        var test = database.AddTest(ContentStatus.Active);
        var question = database.AddQuestion(test.Id, ToeicPart.Part5, 101);
        var learner = Guid.NewGuid();

        database.AddAttempt(learner, test.Id, answers: [(question, true)]);
        database.AddAttempt(learner, test.Id, start: AnalyticsSeed.Recent.AddHours(1), answers: [(question, false)]);

        var answers = await database.EventsAsync(new GetAnalyticsEventsQuery("answer"));

        answers.Rows.Should().HaveCount(2);
        answers.Rows.Select(row => row.Learner).Distinct().Should().ContainSingle()
            .Which.Should().MatchRegex("^[0-9a-f]{16}$");
        answers.Rows.Select(row => row.Correct).Should().Equal(true, false);
        answers.Rows.Should().OnlyContain(row =>
            row.Part == ToeicPart.Part5 && row.Section == ToeicSection.Reading && row.Answered == true && row.QuestionId == question.Id);

        var json = JsonSerializer.Serialize(answers, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        json.Should().NotContain(learner.ToString());
        Regex.IsMatch(json, "correctAnswer|userAnswer", RegexOptions.IgnoreCase).Should().BeFalse();

        var other = AnalyticsSeed.Pseudonymizer("another-deployment-key-0123456789abcdef");
        other.Learner(learner).Should().NotBe(answers.Rows[0].Learner);
    }

    [Fact]
    public async Task Events_OutsideTheRange_AreExcluded()
    {
        using var database = new TestDatabase();
        var test = database.AddTest(ContentStatus.Active);
        database.AddAttempt(Guid.NewGuid(), test.Id, start: new DateTime(2026, 8, 31, 23, 0, 0, DateTimeKind.Utc));
        database.AddAttempt(Guid.NewGuid(), test.Id, start: new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc));

        var result = await database.EventsAsync(new GetAnalyticsEventsQuery("attempt", From: "2026-09-01", To: "2026-09-01"));

        result.Rows.Should().ContainSingle();
        result.NextCursor.Should().BeNull();
    }
}
