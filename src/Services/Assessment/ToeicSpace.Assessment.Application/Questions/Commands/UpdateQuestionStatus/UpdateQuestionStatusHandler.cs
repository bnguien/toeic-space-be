using ToeicSpace.Assessment.Application.Common.Caching;
using ToeicSpace.Assessment.Application.Data;
using ToeicSpace.Assessment.Application.Dtos;
using ToeicSpace.Assessment.Application.Extensions;
using ToeicSpace.Assessment.Application.Questions.Common;
using ToeicSpace.BuildingBlocks.Messaging;

namespace ToeicSpace.Assessment.Application.Questions.Commands.UpdateQuestionStatus;

public sealed class UpdateQuestionStatusHandler : IRequestHandler<UpdateQuestionStatusCommand, QuestionDto>
{
    private readonly IAssessmentDbContext _context;
    private readonly IIntegrationEventPublisher _eventPublisher;
    private readonly ContentCacheInvalidator _cacheInvalidator;
    private readonly TimeProvider _timeProvider;

    public UpdateQuestionStatusHandler(
        IAssessmentDbContext context,
        IIntegrationEventPublisher eventPublisher,
        ContentCacheInvalidator cacheInvalidator,
        TimeProvider timeProvider)
    {
        _context = context;
        _eventPublisher = eventPublisher;
        _cacheInvalidator = cacheInvalidator;
        _timeProvider = timeProvider;
    }

    public async Task<QuestionDto> Handle(
        UpdateQuestionStatusCommand request,
        CancellationToken cancellationToken)
    {
        var question = await _context.ToeicQuestions
            .FirstOrDefaultAsync(item => item.Id == request.Id, cancellationToken)
            ?? throw AppException.NotFound(nameof(ToeicQuestion), request.Id);

        if (question.Status == request.Status)
        {
            return question.ToDto();
        }

        if (request.Status != ContentStatus.Active)
        {
            await PublishedTestGuard.EnsureTestIsNotPublishedAsync(
                _context,
                question.TestId,
                "unpublish a question of this test",
                cancellationToken);
        }

        question.Status = request.Status;

        var updatedAt = _timeProvider.GetUtcNow().UtcDateTime;
        await _eventPublisher.PublishAsync(QuestionIntegrationEvents.Updated(question, updatedAt), cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);

        await _cacheInvalidator.InvalidateQuestionsAsync([question.Id], [question.TestId], cancellationToken);

        return question.ToDto();
    }
}
