using Microsoft.Extensions.Logging;
using ToeicSpace.Content.Application.DTOs.StudySessions;
using ToeicSpace.Content.Application.Interfaces;
using ToeicSpace.Content.Application.Interfaces.Repositories;
using ToeicSpace.Content.Application.Services;
using ToeicSpace.Content.Domain.Enums;
using ToeicSpace.Content.Domain.Exceptions;

namespace ToeicSpace.Content.Application.Features.StudySessions.Commands.CompleteStudySession;

public sealed class CompleteStudySessionHandler(IStudySessionRepository sessions, StudySessionPresenter presenter,
    IUnitOfWork unitOfWork, TimeProvider clock, ILogger<CompleteStudySessionHandler> logger)
    : IRequestHandler<CompleteStudySessionCommand, StudySessionDto>
{
    public async Task<StudySessionDto> Handle(CompleteStudySessionCommand request, CancellationToken ct)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        var session = await sessions.GetForUpdateAsync(request.LearnerId, request.SessionId, ct)
            ?? throw VocabularyErrors.SessionNotFound();
        if (session.Status == StudySessionStatus.Active && !session.IsStale(now))
        {
            session.Complete(now);
            await unitOfWork.SaveChangesAsync(ct);
            logger.LogInformation("Learner {LearnerId} completed session {SessionId} ({Correct}/{Reviews})",
                request.LearnerId, session.Id, session.CorrectCount, session.ReviewCount);
        }
        else if (session.Status != StudySessionStatus.Completed) throw VocabularyErrors.SessionNotActive();
        return await presenter.ToDtoAsync(session, request.TzOffset, ct);
    }
}
