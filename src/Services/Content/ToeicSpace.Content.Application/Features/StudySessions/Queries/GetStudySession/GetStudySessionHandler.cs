using ToeicSpace.Content.Application.DTOs.StudySessions;
using ToeicSpace.Content.Application.Interfaces.Repositories;
using ToeicSpace.Content.Application.Services;
using ToeicSpace.Content.Domain.Exceptions;

namespace ToeicSpace.Content.Application.Features.StudySessions.Queries.GetStudySession;

public sealed class GetStudySessionHandler(IStudySessionRepository sessions, StudySessionPresenter presenter)
    : IRequestHandler<GetStudySessionQuery, StudySessionDto>
{
    public async Task<StudySessionDto> Handle(GetStudySessionQuery request, CancellationToken ct)
    {
        var session = await sessions.GetAsync(request.LearnerId, request.SessionId, ct)
            ?? throw VocabularyErrors.SessionNotFound();
        return await presenter.ToDtoAsync(session, request.TzOffset, ct);
    }
}
