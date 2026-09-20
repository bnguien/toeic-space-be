using Microsoft.Extensions.Logging;
using ToeicSpace.Assessment.Application.Common.Caching;
using ToeicSpace.Assessment.Application.Data;
using ToeicSpace.Assessment.Application.Questions.Common;
using ToeicSpace.BuildingBlocks.Messaging;

namespace ToeicSpace.Assessment.Application.Questions.Commands.DeleteQuestion;

public sealed class DeleteQuestionHandler : IRequestHandler<DeleteQuestionCommand>
{
    private readonly IAssessmentDbContext _context;
    private readonly IIntegrationEventPublisher _eventPublisher;
    private readonly ContentCacheInvalidator _cacheInvalidator;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<DeleteQuestionHandler> _logger;

    public DeleteQuestionHandler(
        IAssessmentDbContext context,
        IIntegrationEventPublisher eventPublisher,
        ContentCacheInvalidator cacheInvalidator,
        TimeProvider timeProvider,
        ILogger<DeleteQuestionHandler> logger)
    {
        _context = context;
        _eventPublisher = eventPublisher;
        _cacheInvalidator = cacheInvalidator;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public async Task Handle(
        DeleteQuestionCommand request,
        CancellationToken cancellationToken)
    {
        var question = await _context.ToeicQuestions
            .FirstOrDefaultAsync(item => item.Id == request.Id, cancellationToken)
            ?? throw AppException.NotFound(nameof(ToeicQuestion), request.Id);

        await PublishedTestGuard.EnsureTestIsNotPublishedAsync(
            _context,
            question.TestId,
            "delete a question of this test",
            cancellationToken);

        await PublishedPracticeSetGuard.EnsureQuestionIsNotUsedByPublishedSetsAsync(
            _context,
            question.Id,
            "delete this question",
            cancellationToken);

        var practiceSetItems = await _context.ToeicPracticeSetItems
            .Where(item => item.QuestionId == question.Id)
            .ToListAsync(cancellationToken);

        var practiceSetIds = practiceSetItems
            .Select(item => item.PracticeSetId)
            .ToList();

        _context.ToeicPracticeSetItems.RemoveRange(practiceSetItems);
        question.MarkAsDeleted(_timeProvider.GetUtcNow().UtcDateTime);

        await _eventPublisher.PublishAsync(QuestionIntegrationEvents.Deleted(question), cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);

        await _cacheInvalidator.InvalidateTestsAsync([question.TestId], cancellationToken);
        await _cacheInvalidator.InvalidatePracticeSetsAsync(practiceSetIds, cancellationToken);

        _logger.LogInformation(
            "Deleted question {QuestionId} and removed it from {PracticeSetCount} practice sets",
            question.Id,
            practiceSetIds.Count);
    }
}
