using Microsoft.Extensions.Logging;
using ToeicSpace.Assessment.Application.Common.Caching;
using ToeicSpace.Assessment.Application.Data;
using ToeicSpace.Assessment.Application.Dtos;
using ToeicSpace.Assessment.Application.Extensions;
using ToeicSpace.Assessment.Application.Questions.Common;
using ToeicSpace.BuildingBlocks.Messaging;

namespace ToeicSpace.Assessment.Application.Questions.Commands.UpdateQuestion;

public sealed class UpdateQuestionHandler : IRequestHandler<UpdateQuestionCommand, QuestionDetailDto>
{
    private readonly IAssessmentDbContext _context;
    private readonly QuestionReferenceGuard _referenceGuard;
    private readonly IIntegrationEventPublisher _eventPublisher;
    private readonly ContentCacheInvalidator _cacheInvalidator;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<UpdateQuestionHandler> _logger;

    public UpdateQuestionHandler(
        IAssessmentDbContext context,
        QuestionReferenceGuard referenceGuard,
        IIntegrationEventPublisher eventPublisher,
        ContentCacheInvalidator cacheInvalidator,
        TimeProvider timeProvider,
        ILogger<UpdateQuestionHandler> logger)
    {
        _context = context;
        _referenceGuard = referenceGuard;
        _eventPublisher = eventPublisher;
        _cacheInvalidator = cacheInvalidator;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public async Task<QuestionDetailDto> Handle(
        UpdateQuestionCommand request,
        CancellationToken cancellationToken)
    {
        var question = await _context.ToeicQuestions
            .FirstOrDefaultAsync(item => item.Id == request.Id, cancellationToken)
            ?? throw AppException.NotFound(nameof(ToeicQuestion), request.Id);

        if (question.Version != request.ExpectedVersion)
        {
            throw AppException.Conflict(
                $"The question was modified by someone else (current version {question.Version}). Reload it and try again.",
                ErrorCodes.ConcurrencyConflict);
        }

        var previousTestId = question.TestId;
        var placementChanged = question.TestId != request.TestId
            || question.PassageId != request.PassageId
            || question.Part != request.Part
            || question.QuestionNumber != request.QuestionNumber;

        if (placementChanged)
        {
            await PublishedTestGuard.EnsureTestIsNotPublishedAsync(_context, previousTestId, "move this question", cancellationToken);
            await PublishedTestGuard.EnsureTestIsNotPublishedAsync(_context, request.TestId, "move this question", cancellationToken);
        }

        if (placementChanged || HasAnswerKeyChanges(question, request))
        {
            await EnsureNotUsedInAttemptsAsync(question.Id, cancellationToken);
        }

        if (question.Part != request.Part)
        {
            var usedInPracticeSets = await _context.ToeicPracticeSetItems
                .AnyAsync(item => item.QuestionId == question.Id, cancellationToken);

            if (usedInPracticeSets)
            {
                throw AppException.Conflict(
                    "Cannot change the part of a question that is used by practice sets.",
                    ErrorCodes.QuestionLocked);
            }
        }

        await _referenceGuard.EnsureValidAsync(request, question.Id, cancellationToken);

        QuestionReferenceGuard.ApplyContent(question, request);
        question.IncreaseVersion();

        var updatedAt = _timeProvider.GetUtcNow().UtcDateTime;
        await _eventPublisher.PublishAsync(QuestionIntegrationEvents.Updated(question, updatedAt), cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);

        await _cacheInvalidator.InvalidateQuestionsAsync([question.Id], [previousTestId, question.TestId], cancellationToken);

        var practiceSetIds = await _context.ToeicPracticeSetItems
            .AsNoTracking()
            .Where(item => item.QuestionId == question.Id)
            .Select(item => item.PracticeSetId)
            .ToListAsync(cancellationToken);

        _logger.LogInformation(
            "Updated question {QuestionId} to version {Version}",
            question.Id,
            question.Version);

        return question.ToDetailDto(practiceSetIds);
    }

    /// <summary>
    /// Changes that would alter how past attempts are interpreted.
    /// </summary>
    private static bool HasAnswerKeyChanges(
        ToeicQuestion question,
        UpdateQuestionCommand request)
    {
        return question.CorrectAnswer != request.CorrectAnswer
            || !string.Equals(question.OptionA, request.OptionA.Trim(), StringComparison.Ordinal)
            || !string.Equals(question.OptionB, request.OptionB.Trim(), StringComparison.Ordinal)
            || !string.Equals(question.OptionC, request.OptionC.Trim(), StringComparison.Ordinal)
            || !string.Equals(question.OptionD ?? string.Empty, request.OptionD?.Trim() ?? string.Empty, StringComparison.Ordinal)
            || !string.Equals(question.QuestionText ?? string.Empty, request.QuestionText?.Trim() ?? string.Empty, StringComparison.Ordinal);
    }

    private async Task EnsureNotUsedInAttemptsAsync(
        Guid questionId,
        CancellationToken cancellationToken)
    {
        var usedInAttempts = await _context.ToeicAttemptAnswers
            .AnyAsync(answer => answer.QuestionId == questionId, cancellationToken);

        if (usedInAttempts)
        {
            throw AppException.Conflict(
                "This question already has learner answers, so its text, options, answer key and placement are locked. Archive it and create a new question instead.",
                ErrorCodes.QuestionLocked);
        }
    }
}
