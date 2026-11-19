using ToeicSpace.Content.Application.DTOs.StudySessions;
using ToeicSpace.Content.Application.Interfaces;
using ToeicSpace.Content.Application.Interfaces.Repositories;
using ToeicSpace.Content.Application.Services;
using ToeicSpace.Content.Domain.Exceptions;
using ToeicSpace.Content.Domain.Rules;

namespace ToeicSpace.Content.Application.Features.StudySessions.Commands.AnswerStudySession;

public sealed class AnswerStudySessionHandler(IStudySessionRepository sessions, ReviewRecorder recorder,
    StudySessionPresenter presenter, IUnitOfWork unitOfWork, TimeProvider clock)
    : IRequestHandler<AnswerStudySessionCommand, StudySessionDto>
{
    public async Task<StudySessionDto> Handle(AnswerStudySessionCommand request, CancellationToken ct)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        var today = SpacedRepetitionScheduler.LocalDay(now, request.TzOffset);
        var session = await sessions.GetForUpdateAsync(request.LearnerId, request.SessionId, ct)
            ?? throw VocabularyErrors.SessionNotFound();

        // A replayed clientEventId returns the session as it is, without advancing it again.
        var replay = await recorder.ReplayAsync(request.LearnerId, request.WordId, request.ClientEventId, today, ct);
        if (replay is not null) return await presenter.ToDtoAsync(session, request.TzOffset, ct, replay);

        if (!session.IsOpen(now)) throw VocabularyErrors.SessionNotActive();
        if (session.CurrentWordId != request.WordId) throw VocabularyErrors.SessionWordMismatch();

        var review = await recorder.StageAsync(request.LearnerId, request.WordId, session.Mode, request.Grade,
            request.ClientEventId, now, today, ct);
        session.Answer(request.WordId, request.Grade, now);
        await unitOfWork.SaveChangesAsync(ct);
        return await presenter.ToDtoAsync(session, request.TzOffset, ct, review);
    }
}
