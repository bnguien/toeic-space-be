using Microsoft.Extensions.Logging.Abstractions;
using ToeicSpace.Assessment.Application.Common.Caching;
using ToeicSpace.Assessment.Application.Questions.Common;
using ToeicSpace.Assessment.Application.Tests.Common;
using ToeicSpace.Assessment.Domain.Rules;
using ToeicSpace.Assessment.Infrastructure.Persistence;
using ToeicSpace.Assessment.Infrastructure.Persistence.Interceptors;

namespace ToeicSpace.Assessment.Application.UnitTests.Support;

/// <summary>
/// An isolated in-memory AssessmentDbContext with the collaborators handlers need.
/// </summary>
public sealed class TestDatabase : IDisposable
{
    public TestDatabase()
    {
        var options = new DbContextOptionsBuilder<AssessmentDbContext>()
            .UseInMemoryDatabase($"assessment-{Guid.NewGuid():N}")
            .AddInterceptors(new AuditableEntityInterceptor(Time))
            .Options;

        Context = new AssessmentDbContext(options);
        CacheInvalidator = new ContentCacheInvalidator(Context, Cache);
        ReferenceGuard = new QuestionReferenceGuard(Context);
        StructureInspector = new TestStructureInspector(Context);
    }

    public AssessmentDbContext Context { get; }

    public FixedTimeProvider Time { get; } = new();

    public FakeEventPublisher Events { get; } = new();

    public InMemoryContentCache Cache { get; } = new();

    public ContentCacheInvalidator CacheInvalidator { get; }

    public QuestionReferenceGuard ReferenceGuard { get; }

    public TestStructureInspector StructureInspector { get; }

    public static NullLogger<T> Logger<T>() => NullLogger<T>.Instance;

    public ToeicTest AddTest(ContentStatus status = ContentStatus.Draft, string code = "ETS-2026-TEST-01")
    {
        var test = new ToeicTest
        {
            Id = Guid.NewGuid(),
            Code = code,
            Title = code,
            Category = "ETS 2026",
            Status = status
        };

        Context.ToeicTests.Add(test);
        Context.SaveChanges();

        return test;
    }

    public ToeicPassage AddPassage(Guid? testId, ToeicPart part, string? content = "W: Hello. M: Hi.")
    {
        var passage = new ToeicPassage
        {
            Id = Guid.NewGuid(),
            TestId = testId,
            Part = part,
            Content = content,
            Transcript = "transcript",
            AudioUrl = part <= ToeicPart.Part4 ? "https://cdn.example.com/audio.mp3" : null
        };

        Context.ToeicPassages.Add(passage);
        Context.SaveChanges();

        return passage;
    }

    public ToeicQuestion AddQuestion(
        Guid? testId,
        ToeicPart part,
        int? questionNumber,
        Guid? passageId = null,
        ContentStatus status = ContentStatus.Active,
        QuestionDifficulty difficulty = QuestionDifficulty.Medium,
        string? questionText = null)
    {
        var question = new ToeicQuestion
        {
            Id = Guid.NewGuid(),
            TestId = testId,
            PassageId = passageId,
            Part = part,
            QuestionNumber = questionNumber,
            QuestionText = questionText ?? $"Question {questionNumber}",
            DifficultyLevel = difficulty,
            ImageUrl = part == ToeicPart.Part1 ? "https://cdn.example.com/photo.jpg" : null,
            OptionA = "A",
            OptionB = "B",
            OptionC = "C",
            OptionD = ToeicPartRules.HasOptionD(part) ? "D" : null,
            CorrectAnswer = AnswerOption.B,
            Explanation = "Because B.",
            Transcript = "Transcript",
            Status = status
        };

        Context.ToeicQuestions.Add(question);
        Context.SaveChanges();

        return question;
    }

    public ToeicPracticeSet AddPracticeSet(
        ToeicPart part,
        ContentStatus status = ContentStatus.Active,
        PracticeSetKind kind = PracticeSetKind.Topic,
        int? level = null,
        string? code = null,
        string title = "Practice")
    {
        var practiceSet = new ToeicPracticeSet
        {
            Id = Guid.NewGuid(),
            Code = code ?? $"p{(int)part}-{kind}-{Guid.NewGuid():N}"[..20],
            Title = title,
            Kind = kind,
            Part = part,
            Level = level,
            TargetScore = level.HasValue ? 350 + (level.Value * 100) : null,
            DurationMinutes = 30,
            Status = status
        };

        Context.ToeicPracticeSets.Add(practiceSet);
        Context.SaveChanges();

        return practiceSet;
    }

    /// <summary>A submitted answer, which locks the content a learner was graded on.</summary>
    public ToeicAttempt AddAttemptAnswer(Guid testId, ToeicQuestion question)
    {
        var attempt = new ToeicAttempt
        {
            Id = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            TestId = testId,
            StartTime = FixedTimeProvider.DefaultNow.UtcDateTime
        };

        attempt.Answers.Add(new ToeicAttemptAnswer
        {
            Id = Guid.NewGuid(),
            QuestionId = question.Id,
            UserAnswer = AnswerOption.A,
            CorrectAnswer = question.CorrectAnswer
        });

        Context.ToeicAttempts.Add(attempt);
        Context.SaveChanges();

        return attempt;
    }

    public void AddPracticeSetItems(Guid practiceSetId, params Guid[] questionIds)
    {
        Context.ToeicPracticeSetItems.AddRange(questionIds.Select((questionId, index) =>
            new ToeicPracticeSetItem { PracticeSetId = practiceSetId, QuestionId = questionId, OrderIndex = index }));
        Context.SaveChanges();
    }

    /// <summary>
    /// Adds the 200 questions of a standard test, with passages for Part 3, 4, 6 and 7.
    /// </summary>
    public void AddStandardQuestions(Guid testId)
    {
        foreach (var part in Enum.GetValues<ToeicPart>())
        {
            var (first, last) = ToeicPartRules.GetStandardQuestionNumberRange(part);
            var passage = ToeicPartRules.RequiresPassage(part) ? AddPassage(testId, part) : null;

            for (var number = first; number <= last; number++)
            {
                AddQuestion(testId, part, number, passage?.Id);
            }
        }
    }

    public void Dispose()
    {
        Context.Dispose();
    }
}
