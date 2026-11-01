using ToeicSpace.Content.Application.DTOs.StudySessions;
using ToeicSpace.Content.Application.Interfaces.Repositories;
using ToeicSpace.Content.Application.Services;
using ToeicSpace.Content.Domain.Entities;
using ToeicSpace.Content.Domain.Enums;

namespace ToeicSpace.Content.Application.Features.StudySessions.Queries.GetActiveStudySession;

public sealed class GetActiveStudySessionHandler(IStudySessionRepository sessions, StudySessionPresenter presenter,
    TimeProvider clock) : IRequestHandler<GetActiveStudySessionQuery, StudySessionDto?>
{
    public async Task<StudySessionDto?> Handle(GetActiveStudySessionQuery request, CancellationToken ct)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        StudySourceKind? kind = null;
        Guid? sourceId = null;
        if (request.SourceCount == 1)
            (kind, sourceId) = StudyQueueService.KindOf(request.SetId, request.FolderId, request.Library, request.Due, null);
        var session = await sessions.GetLatestActiveAsync(request.LearnerId, request.Mode, kind, sourceId,
            now - StudySession.StaleAfter, ct);
        return session is null ? null : await presenter.ToDtoAsync(session, request.TzOffset, ct);
    }
}
