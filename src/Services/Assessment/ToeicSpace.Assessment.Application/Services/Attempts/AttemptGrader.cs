using Microsoft.Extensions.Logging;
using ToeicSpace.Assessment.Application.Interfaces.Persistence;
using ToeicSpace.Assessment.Application.Models.Attempts;
using ToeicSpace.BuildingBlocks.Messaging;
using ToeicSpace.BuildingBlocks.Messaging.Events;

namespace ToeicSpace.Assessment.Application.Services.Attempts;

/// <summary>
/// Submits an attempt: grades every answer from the snapshot answer key, stores the score and completes it.
/// Shared by the learner's submit and the background auto-submit of timed tests.
/// </summary>
public sealed class AttemptGrader
{
    private readonly IAssessmentDbContext _context;
    private readonly IIntegrationEventPublisher _eventPublisher;
    private readonly ILogger<AttemptGrader> _logger;

    public AttemptGrader(
        IAssessmentDbContext context,
        IIntegrationEventPublisher eventPublisher,
        ILogger<AttemptGrader> logger)
    {
        _context = context;
        _eventPublisher = eventPublisher;
        _logger = logger;
    }

    /// <param name="content">Loaded with tracked answers, for a tracked in-progress attempt.</param>
    public async Task SubmitAsync(AttemptContent content, DateTime now, CancellationToken cancellationToken)
    {
        var attempt = content.Attempt;

        if (attempt.Status != AttemptStatus.InProgress)
        {
            throw new InvalidOperationException("Only in-progress attempts can be submitted.");
        }

        foreach (var entry in content.Questions)
        {
            if (entry.Answer is { } answer)
            {
                answer.CorrectAnswer = entry.Question.CorrectAnswer;
                answer.IsCorrect = entry.IsCorrect == true;
            }
        }

        var score = AttemptScoreCalculator.Score(content);

        attempt.TotalQuestions = score.QuestionCount;
        attempt.TotalCorrect = score.Correct;
        attempt.ListeningCorrect = score.ListeningCorrect;
        attempt.ReadingCorrect = score.ReadingCorrect;
        attempt.ListeningScore = score.ListeningScaled ?? 0;
        attempt.ReadingScore = score.ReadingScaled ?? 0;
        attempt.TotalScore = score.TotalScaled ?? 0;
        attempt.DurationSeconds = Duration(content, now);
        attempt.EndTime = now;
        attempt.LastActivityAt = now;
        attempt.Status = AttemptStatus.Completed;

        if (attempt.Mode == AttemptMode.FullTest && attempt.TestId is { } testId)
        {
            var source = content.Source;

            // Stored in the transactional outbox and delivered only if the submission commits.
            await _eventPublisher.PublishAsync(new ExamAttemptSubmittedIntegrationEvent(
                attempt.Id,
                attempt.UserId,
                testId,
                source.Code,
                source.Title,
                source.Category ?? string.Empty,
                attempt.ListeningScore,
                attempt.ReadingScore,
                attempt.TotalScore,
                attempt.TotalCorrect,
                attempt.TotalQuestions,
                attempt.DurationSeconds,
                AttemptAccess.Utc(now)), cancellationToken);
        }

        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Submitted attempt {AttemptId} ({Mode}) with {Correct}/{Total} correct",
            attempt.Id,
            attempt.Mode,
            attempt.TotalCorrect,
            attempt.TotalQuestions);
    }

    /// <summary>
    /// FullTest: time used up to the deadline. Practice: the time spent on questions, or the wall clock when the
    /// client never reported any.
    /// </summary>
    private static int Duration(AttemptContent content, DateTime now)
    {
        var attempt = content.Attempt;

        if (attempt.Mode == AttemptMode.FullTest)
        {
            var end = attempt.Deadline is { } deadline && deadline < now ? deadline : now;
            return Seconds(end - attempt.StartTime);
        }

        var spent = content.Questions.Sum(entry => entry.Answer?.TimeSpentSeconds ?? 0);

        return spent > 0 ? spent : Seconds(now - attempt.StartTime);
    }

    private static int Seconds(TimeSpan span)
        => (int)Math.Clamp(Math.Floor(span.TotalSeconds), 0, int.MaxValue);
}
