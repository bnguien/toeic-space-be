using ToeicSpace.Assessment.Application.Features.Attempts.Commands.StartPracticeSetAttempt;
using ToeicSpace.Assessment.Application.Models.Attempts;
using ToeicSpace.Assessment.Application.Services.Attempts;

namespace ToeicSpace.Assessment.Application.UnitTests.Attempts;

public sealed class PracticeSelectionTests
{
    // Units: [q1], [q2,q3 | passage P], [q4], [q5,q6 | passage R]
    private static readonly Guid[] Q = Enumerable.Range(0, 7).Select(_ => Guid.NewGuid()).ToArray();
    private static readonly Guid P = Guid.NewGuid();
    private static readonly Guid R = Guid.NewGuid();
    private static readonly IReadOnlyList<SelectionCandidate> Candidates =
    [
        new(Q[1], null), new(Q[2], P), new(Q[3], P), new(Q[4], null), new(Q[5], R), new(Q[6], R)
    ];

    [Fact]
    public void Continue_TakesNeverAnsweredUnitsInSetOrder_KeepingPassagesWhole()
    {
        var plan = PracticeSelectionPlanner.Plan(Candidates, PracticeSelection.Continue, count: 2,
            answered: new HashSet<Guid> { Q[1], Q[2] }, wrong: new HashSet<Guid>(), new Random(1));

        plan.Should().Equal(Q[2], Q[3]);
    }

    [Fact]
    public void Random_PutsNeverAnsweredUnitsFirst()
    {
        var answered = new HashSet<Guid> { Q[1], Q[2], Q[3], Q[4] };

        for (var seed = 0; seed < 20; seed++)
        {
            var plan = PracticeSelectionPlanner.Plan(Candidates, PracticeSelection.Random, count: 2,
                answered, new HashSet<Guid>(), new Random(seed));

            plan.Should().Equal(Q[5], Q[6]);
        }

        var all = PracticeSelectionPlanner.Plan(Candidates, PracticeSelection.Random, count: 200,
            answered, new HashSet<Guid>(), new Random(3)).ToList();
        all.Should().BeEquivalentTo(Q.Skip(1));
        all.Take(2).Should().Equal(Q[5], Q[6]);
        all.IndexOf(Q[3]).Should().Be(all.IndexOf(Q[2]) + 1, "passage groups stay together");
    }

    [Fact]
    public void Mistakes_TakesUnitsWithAWrongLatestAnswer()
    {
        var plan = PracticeSelectionPlanner.Plan(Candidates, PracticeSelection.Mistakes, count: null,
            answered: new HashSet<Guid>(Q), wrong: new HashSet<Guid> { Q[3], Q[4] }, new Random(1));

        plan.Should().Equal(Q[2], Q[3], Q[4]);
    }

    [Fact]
    public async Task RandomSession_OnAPracticeSet_HasAGeneratedTitle()
    {
        using var h = new AttemptTestHarness();
        var set = AddPart5Set(h, 30);

        var attempt = await h.StartSet(set.Id, PracticeSelection.Random, count: 20);

        attempt.Questions.Should().HaveCount(20);
        attempt.Source.Should().BeEquivalentTo(new { Kind = AttemptSourceKind.PracticeSet, Id = set.Id, Part = 5 });
        attempt.PracticeSetId.Should().Be(set.Id);
        attempt.Title.Should().Be("Part 5 · 20 câu ngẫu nhiên · 14/09/2026");
    }

    [Fact]
    public async Task ContinueSession_SkipsQuestionsAnsweredBefore()
    {
        using var h = new AttemptTestHarness();
        var set = AddPart5Set(h, 12);
        var first = await h.StartSet(set.Id, PracticeSelection.Continue, count: 5);
        await h.AnswerAll(first, _ => true);
        await h.Submit(first.Id);

        var second = await h.StartSet(set.Id, PracticeSelection.Continue, count: 5);

        second.Title.Should().Be("Part 5 · Học tiếp");
        second.Questions.Select(question => question.SourceQuestionId)
            .Should().NotIntersectWith(first.Questions.Select(question => question.SourceQuestionId));
        second.Questions.Select(question => question.SourceQuestionId).Should().Equal(
            h.Db.Context.ToeicPracticeSetItems.Where(item => item.PracticeSetId == set.Id)
                .OrderBy(item => item.OrderIndex).Skip(5).Take(5).Select(item => item.QuestionId));
    }

    [Fact]
    public async Task MistakesSession_WithoutMistakes_HasNothingToPractice()
    {
        using var h = new AttemptTestHarness();
        var set = AddPart5Set(h, 6);

        var start = () => h.StartSet(set.Id, PracticeSelection.Mistakes);

        (await start.Should().ThrowAsync<AppException>()).Which.Code.Should().Be(ErrorCodes.NothingToPractice);
    }

    [Fact]
    public async Task MistakesSession_UsesTheLatestCheckedAnswer()
    {
        using var h = new AttemptTestHarness();
        var set = AddPart5Set(h, 6);
        var first = await h.StartSet(set.Id);
        await h.Save(first.Id, first.Questions[0].Id, AnswerOption.A);
        await h.Save(first.Id, first.Questions[1].Id, AnswerOption.B);
        await h.Check(first.Id, first.Questions[0].Id, first.Questions[1].Id);

        var mistakes = await h.StartSet(set.Id, PracticeSelection.Mistakes, generation: 0, restart: true);

        mistakes.Questions.Should().ContainSingle().Which.SourceQuestionId.Should().Be(first.Questions[0].SourceQuestionId);
        mistakes.Title.Should().Be("Part 5 · Ôn câu sai");
        (await h.Db.Context.ToeicAttempts.SingleAsync(attempt => attempt.Id == first.Id)).Status.Should().Be(AttemptStatus.Abandoned);
    }

    [Fact]
    public async Task StartWithAStaleGeneration_IsRejected()
    {
        using var h = new AttemptTestHarness();
        var set = AddPart5Set(h, 6);

        var start = () => h.StartSet(set.Id, generation: 3);

        (await start.Should().ThrowAsync<AppException>()).Which.Code.Should().Be(ErrorCodes.StaleStartGeneration);
    }

    [Theory]
    [InlineData(4, false)]
    [InlineData(5, true)]
    [InlineData(200, true)]
    [InlineData(201, false)]
    public void Validator_BoundsTheCount(int count, bool valid)
        => new StartPracticeSetAttemptValidator()
            .Validate(new StartPracticeSetAttemptCommand(Guid.NewGuid(), 0, PracticeSelection.Random, count))
            .IsValid.Should().Be(valid);

    internal static ToeicPracticeSet AddPart5Set(AttemptTestHarness h, int questions)
    {
        var set = h.Db.AddPracticeSet(ToeicPart.Part5, kind: PracticeSetKind.PartDrill, title: "Part 5 drill");
        var ids = Enumerable.Range(1, questions)
            .Select(number => h.Db.AddQuestion(null, ToeicPart.Part5, null, questionText: $"Sentence {number} -------.").Id)
            .ToArray();
        h.Db.AddPracticeSetItems(set.Id, ids);
        return set;
    }
}
