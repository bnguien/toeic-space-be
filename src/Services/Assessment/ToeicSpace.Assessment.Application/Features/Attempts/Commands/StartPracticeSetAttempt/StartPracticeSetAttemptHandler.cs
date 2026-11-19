using Microsoft.Extensions.Logging;
using ToeicSpace.Assessment.Application.DTOs.Attempts;
using ToeicSpace.Assessment.Application.Interfaces.Persistence;
using ToeicSpace.Assessment.Application.Interfaces.Storage;
using ToeicSpace.Assessment.Application.Models.Attempts;
using ToeicSpace.Assessment.Application.Services;
using ToeicSpace.Assessment.Application.Services.Attempts;

namespace ToeicSpace.Assessment.Application.Features.Attempts.Commands.StartPracticeSetAttempt;

public sealed class StartPracticeSetAttemptHandler : IRequestHandler<StartPracticeSetAttemptCommand, AttemptDto>
{
    private readonly IAssessmentDbContext _context;
    private readonly AttemptAccess _access;
    private readonly AttemptSourceReader _sources;
    private readonly AttemptDocumentReader _documents;
    private readonly AnswerHistoryReader _history;
    private readonly IAttemptMediaStore _media;
    private readonly ILogger<StartPracticeSetAttemptHandler> _logger;

    public StartPracticeSetAttemptHandler(
        IAssessmentDbContext context,
        AttemptAccess access,
        AttemptSourceReader sources,
        AttemptDocumentReader documents,
        AnswerHistoryReader history,
        IAttemptMediaStore media,
        ILogger<StartPracticeSetAttemptHandler> logger)
    {
        _context = context;
        _access = access;
        _sources = sources;
        _documents = documents;
        _history = history;
        _media = media;
        _logger = logger;
    }

    public async Task<AttemptDto> Handle(StartPracticeSetAttemptCommand request, CancellationToken cancellationToken)
    {
        var owner = _access.RequireLearner();
        var setId = request.PracticeSetId;

        // Cheap retry/expired path BEFORE any external media work.
        var existing = await _context.ToeicAttempts
            .AsNoTracking()
            .Where(attempt => attempt.UserId == owner
                && attempt.PracticeSetId == setId
                && attempt.Status == AttemptStatus.InProgress)
            .Select(attempt => (Guid?)attempt.Id)
            .SingleOrDefaultAsync(cancellationToken);

        if (existing is { } existingId && !request.Restart)
        {
            var resumed = await _context.ExecuteAttemptUpdateAsync(owner, setId, async () =>
            {
                var state = await _access.FenceAsync(owner, setId, cancellationToken);
                EnsureGeneration(state, request);
                var current = await _access.Eligible()
                    .SingleOrDefaultAsync(attempt => attempt.Id == existingId && attempt.UserId == owner, cancellationToken);

                if (current is null || current.Status != AttemptStatus.InProgress)
                {
                    return null;
                }

                if (_access.IsExpired(current))
                {
                    await _access.DeleteAsync(current, state, cancellationToken);
                    return null;
                }

                // A start retry is NOT a resume: activity is not refreshed.
                return await _documents.ReadAsync(current, cancellationToken);
            }, cancellationToken);

            return resumed ?? throw AttemptAccess.Unavailable();
        }

        // Prepare copies outside all DB locks. Revalidate the exact source inside the transaction.
        var generation = await _context.PracticeAttemptStates
            .AsNoTracking()
            .Where(state => state.UserId == owner && state.PracticeSetId == setId)
            .Select(state => (long?)state.Generation)
            .SingleOrDefaultAsync(cancellationToken) ?? 0;

        if (generation != request.ExpectedGeneration)
        {
            throw StaleStart();
        }

        var before = await _sources.ReadPracticeSetAsync(setId, cancellationToken);
        await PracticeSetPublishingGuard.EnsureValidAsync(_context, before.Set!, before.Order, cancellationToken, lockContent: false);
        var selected = await SelectAsync(owner, before, request, cancellationToken);

        if (selected.Count == 0)
        {
            throw AttemptAccess.NothingToPractice();
        }

        var media = await _media.PreserveAsync(AttemptSourceReader.MediaReferences(before, selected), cancellationToken);

        var result = await _context.ExecuteAttemptUpdateAsync(owner, setId, async () =>
        {
            var state = await _access.FenceAsync(owner, setId, cancellationToken);
            EnsureGeneration(state, request);
            var active = await _access.InProgressFor(owner, setId).SingleOrDefaultAsync(cancellationToken);

            if (active is not null)
            {
                if (_access.IsExpired(active))
                {
                    await _access.DeleteAsync(active, state, cancellationToken);

                    if (!request.Restart)
                    {
                        return null;
                    }
                }
                else if (request.Restart)
                {
                    await _access.AbandonAsync(active, state, cancellationToken);
                }
                else
                {
                    return await _documents.ReadAsync(active, cancellationToken);
                }
            }

            await _context.LockAttemptSetAsync(setId, cancellationToken);
            var ids = await _context.ToeicPracticeSetItems
                .Where(item => item.PracticeSetId == setId)
                .OrderBy(item => item.OrderIndex)
                .Select(item => item.QuestionId)
                .ToListAsync(cancellationToken);
            await _context.LockPracticeContentAsync(ids, cancellationToken);
            var current = await _sources.ReadPracticeSetAsync(setId, cancellationToken);

            if (!AttemptSourceReader.SameContent(before, current))
            {
                throw AppException.Conflict("Practice content changed while media was prepared. Start again.",
                    ErrorCodes.ConcurrencyConflict);
            }

            await PracticeSetPublishingGuard.EnsureValidAsync(_context, current.Set!, ids, cancellationToken, lockContent: false);

            var now = _access.Now;
            var set = AttemptSnapshotFactory.Capture(current.Set!);
            var (questions, passages) = AttemptSnapshotFactory.Capture(current, selected, media);
            var attempt = AttemptSnapshotFactory.Add(_context, new NewAttempt(
                owner,
                null,
                setId,
                AttemptMode.Practice,
                AttemptTitles.ForPracticeSet(set, request.Selection, questions.Count, now),
                new AttemptSnapshotDocument(set, passages, AttemptSnapshotFactory.Source(set)),
                questions,
                null,
                now));

            await _context.SaveChangesAsync(cancellationToken);

            _logger.LogInformation(
                "Started practice attempt {AttemptId} on set {PracticeSetId} ({Selection}, {QuestionCount} questions)",
                attempt.Id, setId, request.Selection, questions.Count);

            return await _documents.ReadAsync(attempt, cancellationToken);
        }, cancellationToken);

        return result ?? throw AttemptAccess.Unavailable(); // Deletion has committed before this error is raised.
    }

    private async Task<IReadOnlyList<Guid>> SelectAsync(
        Guid owner,
        AttemptSourceContent source,
        StartPracticeSetAttemptCommand request,
        CancellationToken cancellationToken)
    {
        if (request.Selection == PracticeSelection.All)
        {
            return source.Order;
        }

        var passageOf = source.Questions.ToDictionary(question => question.Id, question => question.PassageId);
        var candidates = source.Order
            .Select(id => new SelectionCandidate(id, passageOf.GetValueOrDefault(id)))
            .ToList();
        var answered = await _history.AnsweredQuestionIdsAsync(owner, source.Order, cancellationToken);
        var latest = await _history.LatestGradedAsync(owner, source.Order, cancellationToken);
        var wrong = latest.Values.Where(answer => !answer.IsCorrect).Select(answer => answer.SourceQuestionId).ToHashSet();

        return PracticeSelectionPlanner.Plan(candidates, request.Selection, request.Count, answered, wrong, Random.Shared);
    }

    private static void EnsureGeneration(PracticeAttemptState state, StartPracticeSetAttemptCommand request)
    {
        if (state.Generation != request.ExpectedGeneration)
        {
            throw StaleStart();
        }
    }

    private static AppException StaleStart()
        => AppException.Conflict("Start request has expired. Reload the practice set.", ErrorCodes.StaleStartGeneration);
}
