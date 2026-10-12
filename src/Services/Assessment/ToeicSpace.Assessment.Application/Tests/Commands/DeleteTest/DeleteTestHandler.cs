using Microsoft.Extensions.Logging;
using ToeicSpace.Assessment.Application.Common.Caching;
using ToeicSpace.Assessment.Application.Data;
using ToeicSpace.Assessment.Application.Questions.Common;
using ToeicSpace.BuildingBlocks.Messaging;

namespace ToeicSpace.Assessment.Application.Tests.Commands.DeleteTest;

/// <summary>
/// Soft deletes a test. Questions still used by practice sets are kept in the bank (detached
/// from the test); every other question and passage of the test is soft deleted with it.
/// </summary>
public sealed class DeleteTestHandler : IRequestHandler<DeleteTestCommand>
{
    private readonly IAssessmentDbContext _context;
    private readonly IIntegrationEventPublisher _eventPublisher;
    private readonly ContentCacheInvalidator _cacheInvalidator;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<DeleteTestHandler> _logger;

    public DeleteTestHandler(
        IAssessmentDbContext context,
        IIntegrationEventPublisher eventPublisher,
        ContentCacheInvalidator cacheInvalidator,
        TimeProvider timeProvider,
        ILogger<DeleteTestHandler> logger)
    {
        _context = context;
        _eventPublisher = eventPublisher;
        _cacheInvalidator = cacheInvalidator;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public async Task Handle(
        DeleteTestCommand request,
        CancellationToken cancellationToken)
    {
        var test = await _context.ToeicTests
            .FirstOrDefaultAsync(item => item.Id == request.Id, cancellationToken)
            ?? throw AppException.NotFound(nameof(ToeicTest), request.Id);

        var now = _timeProvider.GetUtcNow().UtcDateTime;

        var questions = await _context.ToeicQuestions
            .Where(question => question.TestId == test.Id)
            .ToListAsync(cancellationToken);

        var questionIds = questions.Select(question => question.Id).ToList();

        var reusedQuestionIds = (await _context.ToeicPracticeSetItems
            .Where(item => questionIds.Contains(item.QuestionId))
            .Select(item => item.QuestionId)
            .Distinct()
            .ToListAsync(cancellationToken))
            .ToHashSet();

        var passages = await _context.ToeicPassages
            .Where(passage => passage.TestId == test.Id)
            .ToListAsync(cancellationToken);

        var keptPassageIds = questions
            .Where(question => reusedQuestionIds.Contains(question.Id) && question.PassageId.HasValue)
            .Select(question => question.PassageId!.Value)
            .ToHashSet();

        foreach (var question in questions)
        {
            if (reusedQuestionIds.Contains(question.Id))
            {
                question.TestId = null;
                question.QuestionNumber = null;
                await _eventPublisher.PublishAsync(QuestionIntegrationEvents.Updated(question, now), cancellationToken);
            }
            else
            {
                question.MarkAsDeleted(now);
                await _eventPublisher.PublishAsync(QuestionIntegrationEvents.Deleted(question), cancellationToken);
            }
        }

        foreach (var passage in passages)
        {
            if (keptPassageIds.Contains(passage.Id))
            {
                passage.TestId = null;
            }
            else
            {
                passage.MarkAsDeleted(now);
            }
        }

        test.MarkAsDeleted(now);
        await _context.SaveChangesAsync(cancellationToken);

        await _cacheInvalidator.InvalidateQuestionsAsync(questionIds, [test.Id], cancellationToken);

        _logger.LogInformation(
            "Deleted test {TestId}: {DeletedCount} questions deleted, {KeptCount} kept in the bank for practice sets",
            test.Id,
            questions.Count - reusedQuestionIds.Count,
            reusedQuestionIds.Count);
    }
}
