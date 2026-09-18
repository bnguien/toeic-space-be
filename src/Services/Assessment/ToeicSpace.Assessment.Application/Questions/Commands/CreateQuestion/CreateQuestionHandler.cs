using Microsoft.Extensions.Logging;
using ToeicSpace.Assessment.Application.Common.Caching;
using ToeicSpace.Assessment.Application.Data;
using ToeicSpace.Assessment.Application.Dtos;
using ToeicSpace.Assessment.Application.Extensions;
using ToeicSpace.Assessment.Application.Interfaces.Security;
using ToeicSpace.Assessment.Application.Questions.Common;
using ToeicSpace.BuildingBlocks.Messaging;

namespace ToeicSpace.Assessment.Application.Questions.Commands.CreateQuestion;

public sealed class CreateQuestionHandler : IRequestHandler<CreateQuestionCommand, QuestionDetailDto>
{
    private readonly IAssessmentDbContext _context;
    private readonly QuestionReferenceGuard _referenceGuard;
    private readonly IIntegrationEventPublisher _eventPublisher;
    private readonly ContentCacheInvalidator _cacheInvalidator;
    private readonly ICurrentUserService _currentUser;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<CreateQuestionHandler> _logger;

    public CreateQuestionHandler(
        IAssessmentDbContext context,
        QuestionReferenceGuard referenceGuard,
        IIntegrationEventPublisher eventPublisher,
        ContentCacheInvalidator cacheInvalidator,
        ICurrentUserService currentUser,
        TimeProvider timeProvider,
        ILogger<CreateQuestionHandler> logger)
    {
        _context = context;
        _referenceGuard = referenceGuard;
        _eventPublisher = eventPublisher;
        _cacheInvalidator = cacheInvalidator;
        _currentUser = currentUser;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public async Task<QuestionDetailDto> Handle(
        CreateQuestionCommand request,
        CancellationToken cancellationToken)
    {
        await _referenceGuard.EnsureValidAsync(request, currentQuestionId: null, cancellationToken);
        await PublishedTestGuard.EnsureTestIsNotPublishedAsync(
            _context,
            request.TestId,
            "add a question to this test",
            cancellationToken);

        var question = new ToeicQuestion
        {
            Id = Guid.NewGuid(),
            Status = ContentStatus.Active,
            Version = 1,
            CreatedByUserId = _currentUser.UserId,
            CreatedAt = _timeProvider.GetUtcNow().UtcDateTime
        };

        QuestionReferenceGuard.ApplyContent(question, request);

        _context.ToeicQuestions.Add(question);

        // Stored in the transactional outbox and delivered after the save succeeds.
        await _eventPublisher.PublishAsync(QuestionIntegrationEvents.Created(question), cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);

        await _cacheInvalidator.InvalidateTestsAsync([question.TestId], cancellationToken);

        _logger.LogInformation(
            "Created question {QuestionId} for test {TestId}",
            question.Id,
            question.TestId);

        return question.ToDetailDto([]);
    }
}
