using Microsoft.EntityFrameworkCore;
using ToeicSpace.Content.Application.Interfaces.Repositories;
using ToeicSpace.Content.Domain.Entities;
using ToeicSpace.Content.Domain.Enums;

namespace ToeicSpace.Content.Infrastructure.Persistence.Repositories;

public sealed class StudySessionRepository(ContentDbContext db) : IStudySessionRepository
{
    public Task<StudySession?> GetAsync(Guid learnerId, Guid sessionId, CancellationToken ct) =>
        PersistenceExceptionTranslator.ExecuteAsync(() => db.StudySessions.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == sessionId && x.LearnerId == learnerId, ct));

    public Task<StudySession?> GetForUpdateAsync(Guid learnerId, Guid sessionId, CancellationToken ct) =>
        PersistenceExceptionTranslator.ExecuteAsync(() => db.StudySessions
            .FirstOrDefaultAsync(x => x.Id == sessionId && x.LearnerId == learnerId, ct));

    public Task<StudySession?> GetActiveBySlotForUpdateAsync(Guid learnerId, string slot, CancellationToken ct) =>
        PersistenceExceptionTranslator.ExecuteAsync(() => db.StudySessions
            .FirstOrDefaultAsync(x => x.LearnerId == learnerId && x.ActiveSlot == slot, ct));

    public Task<StudySession?> GetLatestActiveAsync(Guid learnerId, StudyMode? mode, StudySourceKind? kind,
        Guid? sourceId, DateTime activeSince, CancellationToken ct) =>
        PersistenceExceptionTranslator.ExecuteAsync(() =>
        {
            var query = db.StudySessions.AsNoTracking().Where(x => x.LearnerId == learnerId
                && x.Status == StudySessionStatus.Active && x.UpdatedAt >= activeSince);
            if (mode is { } m) query = query.Where(x => x.Mode == m);
            if (kind is { } k) query = query.Where(x => x.SourceKind == k && x.SourceId == sourceId);
            return query.OrderByDescending(x => x.UpdatedAt).ThenBy(x => x.Id).FirstOrDefaultAsync(ct);
        });

    public void Add(StudySession session) => db.StudySessions.Add(session);
}
