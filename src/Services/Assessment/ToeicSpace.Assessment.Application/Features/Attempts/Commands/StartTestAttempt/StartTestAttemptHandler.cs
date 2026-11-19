using System.Diagnostics;
using Microsoft.Extensions.Logging;
using ToeicSpace.Assessment.Application.DTOs.Attempts;
using ToeicSpace.Assessment.Application.Interfaces.Persistence;
using ToeicSpace.Assessment.Application.Interfaces.Storage;
using ToeicSpace.Assessment.Application.Models.Attempts;
using ToeicSpace.Assessment.Application.Services.Attempts;

namespace ToeicSpace.Assessment.Application.Features.Attempts.Commands.StartTestAttempt;

public sealed class StartTestAttemptHandler : IRequestHandler<StartTestAttemptCommand, AttemptDto>
{
    private readonly IAssessmentDbContext _context;
    private readonly AttemptAccess _access;
    private readonly AttemptSourceReader _sources;
    private readonly AttemptDocumentReader _documents;
    private readonly IAttemptMediaStore _media;
    private readonly ILogger<StartTestAttemptHandler> _logger;

    public StartTestAttemptHandler(
        IAssessmentDbContext context,
        AttemptAccess access,
        AttemptSourceReader sources,
        AttemptDocumentReader documents,
        IAttemptMediaStore media,
        ILogger<StartTestAttemptHandler> logger)
    {
        _context = context;
        _access = access;
        _sources = sources;
        _documents = documents;
        _media = media;
        _logger = logger;
    }

    public async Task<AttemptDto> Handle(StartTestAttemptCommand request, CancellationToken cancellationToken)
    {
        var owner = _access.RequireLearner();
        var mode = request.Mode!.Value;
        IReadOnlyList<ToeicPart> parts = mode == AttemptMode.FullTest
            ? AttemptParts.All
            : request.Parts!.Distinct().Order().Select(part => (ToeicPart)part).ToList();

        // Cheap resume path before any external media work.
        if (!request.Restart)
        {
            var resumed = await TryResumeAsync(owner, request.TestId, cancellationToken);

            if (resumed is not null)
            {
                return resumed;
            }
        }

        // Media are prepared outside all database locks; the source is re-read and compared under the locks.
        var before = await _sources.ReadTestAsync(request.TestId, parts, cancellationToken);

        if (before.Order.Count == 0)
        {
            throw AttemptAccess.NothingToPractice();
        }

        var watch = Stopwatch.StartNew();
        var media = await _media.PreserveAsync(AttemptSourceReader.MediaReferences(before, before.Order), cancellationToken);
        _logger.LogInformation(
            "Prepared media for test {TestId} ({Mode}, {QuestionCount} questions) in {ElapsedMs}ms",
            request.TestId, mode, before.Order.Count, watch.ElapsedMilliseconds);

        return await _context.ExecuteAttemptUpdateAsync(owner, request.TestId, async () =>
        {
            var state = await _access.FenceAsync(owner, request.TestId, cancellationToken);
            var active = await _access.InProgressFor(owner, request.TestId).SingleOrDefaultAsync(cancellationToken);

            if (active is not null)
            {
                if (request.Restart)
                {
                    await _access.AbandonAsync(active, state, cancellationToken);
                }
                else if (_access.IsExpired(active))
                {
                    await _access.DeleteAsync(active, state, cancellationToken);
                }
                else
                {
                    // Another request started the test meanwhile: resume it.
                    return await ResumeAsync(active, cancellationToken);
                }
            }

            await _context.LockAttemptTestAsync(request.TestId, cancellationToken);
            await _context.LockPracticeContentAsync(before.Order, cancellationToken);
            var current = await _sources.ReadTestAsync(request.TestId, parts, cancellationToken);

            if (!AttemptSourceReader.SameContent(before, current))
            {
                throw AppException.Conflict("Test content changed while media was prepared. Start again.",
                    ErrorCodes.ConcurrencyConflict);
            }

            var test = current.Test!;
            var (questions, passages) = AttemptSnapshotFactory.Capture(current, current.Order, media);
            var attempt = AttemptSnapshotFactory.Add(_context, new NewAttempt(
                owner,
                test.Id,
                null,
                mode,
                AttemptTitles.ForTest(test.Title, mode, parts),
                new AttemptSnapshotDocument(null, passages, AttemptSnapshotFactory.Source(test)),
                questions,
                mode == AttemptMode.FullTest ? test.DurationMinutes * 60 : null,
                _access.Now));

            await _context.SaveChangesAsync(cancellationToken);

            _logger.LogInformation(
                "Started {Mode} attempt {AttemptId} on test {TestId} with {QuestionCount} questions",
                attempt.Mode, attempt.Id, test.Id, questions.Count);

            return await _documents.ReadAsync(attempt, cancellationToken);
        }, cancellationToken);
    }

    private async Task<AttemptDto?> TryResumeAsync(Guid owner, Guid testId, CancellationToken cancellationToken)
    {
        var exists = await _context.ToeicAttempts
            .AsNoTracking()
            .AnyAsync(attempt => attempt.UserId == owner
                && attempt.TestId == testId
                && attempt.Status == AttemptStatus.InProgress, cancellationToken);

        if (!exists)
        {
            return null;
        }

        return await _context.ExecuteAttemptUpdateAsync(owner, testId, async () =>
        {
            var state = await _access.FenceAsync(owner, testId, cancellationToken);
            var active = await _access.InProgressFor(owner, testId).SingleOrDefaultAsync(cancellationToken);

            if (active is null)
            {
                return null;
            }

            if (_access.IsExpired(active))
            {
                await _access.DeleteAsync(active, state, cancellationToken);
                return null;
            }

            return await ResumeAsync(active, cancellationToken);
        }, cancellationToken);
    }

    private async Task<AttemptDto> ResumeAsync(ToeicAttempt attempt, CancellationToken cancellationToken)
    {
        attempt.LastActivityAt = _access.Now;
        await _context.SaveChangesAsync(cancellationToken);

        return await _documents.ReadAsync(attempt, cancellationToken);
    }
}
