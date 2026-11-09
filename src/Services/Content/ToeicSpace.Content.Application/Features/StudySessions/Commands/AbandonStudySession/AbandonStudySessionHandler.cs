using ToeicSpace.Content.Application.Interfaces;
using ToeicSpace.Content.Application.Interfaces.Repositories;
using ToeicSpace.Content.Domain.Enums;
using ToeicSpace.Content.Domain.Exceptions;

namespace ToeicSpace.Content.Application.Features.StudySessions.Commands.AbandonStudySession;

public sealed class AbandonStudySessionHandler(IStudySessionRepository sessions, IUnitOfWork unitOfWork,
    TimeProvider clock) : IRequestHandler<AbandonStudySessionCommand>
{
    public async Task Handle(AbandonStudySessionCommand request, CancellationToken ct)
    {
        var session = await sessions.GetForUpdateAsync(request.LearnerId, request.SessionId, ct)
            ?? throw VocabularyErrors.SessionNotFound();
        if (session.Status != StudySessionStatus.Active) return;
        session.Abandon(clock.GetUtcNow().UtcDateTime);
        await unitOfWork.SaveChangesAsync(ct);
    }
}
