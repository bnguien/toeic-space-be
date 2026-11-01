using Microsoft.Extensions.Logging;
using ToeicSpace.Content.Application.DTOs.StudySessions;
using ToeicSpace.Content.Application.Interfaces;
using ToeicSpace.Content.Application.Interfaces.Repositories;
using ToeicSpace.Content.Application.Services;
using ToeicSpace.Content.Domain.Entities;
using ToeicSpace.Content.Domain.Enums;
using ToeicSpace.Content.Domain.Exceptions;
using ToeicSpace.Content.Domain.Rules;

namespace ToeicSpace.Content.Application.Features.StudySessions.Commands.StartStudySession;

public sealed class StartStudySessionHandler(IStudySessionRepository sessions, StudyQueueService queue,
    StudySessionPresenter presenter, IUnitOfWork unitOfWork, TimeProvider clock,
    ILogger<StartStudySessionHandler> logger) : IRequestHandler<StartStudySessionCommand, StudySessionStartResult>
{
    public async Task<StudySessionStartResult> Handle(StartStudySessionCommand request, CancellationToken ct)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        var today = SpacedRepetitionScheduler.LocalDay(now, request.TzOffset);
        var (kind, sourceId) = StudyQueueService.KindOf(request.SetId, request.FolderId, request.Library, request.Due,
            request.WordIds);

        var current = await sessions.GetActiveBySlotForUpdateAsync(request.LearnerId,
            StudySession.SlotOf(request.Mode, kind, sourceId), ct);
        if (current is not null && !request.Restart && kind != StudySourceKind.Words && !current.IsStale(now))
            return new StudySessionStartResult(await presenter.ToDtoAsync(current, request.TzOffset, ct), false);

        var source = await queue.ResolveAsync(request.LearnerId, kind, sourceId, request.WordIds, request.Limit, today, ct);
        if (source.WordIds.Count == 0) throw VocabularyErrors.SessionEmpty();
        if (current is not null)
        {
            // Restart, word list or expired: free the slot before the new session takes it.
            current.Abandon(now);
            await unitOfWork.SaveChangesAsync(ct);
        }

        var session = new StudySession
        {
            LearnerId = request.LearnerId, Mode = request.Mode, SourceKind = kind, SourceId = source.Id,
            SourceTitle = source.Title, Queue = source.WordIds.ToList()
        };
        session.Activate(now);
        sessions.Add(session);
        await unitOfWork.SaveChangesAsync(ct);
        logger.LogInformation("Learner {LearnerId} started {Mode} session {SessionId} on {Kind} with {Count} words",
            request.LearnerId, request.Mode, session.Id, kind, session.Queue.Count);
        return new StudySessionStartResult(await presenter.ToDtoAsync(session, request.TzOffset, ct), true);
    }
}
