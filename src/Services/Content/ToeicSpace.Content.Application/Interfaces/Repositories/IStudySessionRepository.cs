using ToeicSpace.Content.Domain.Entities;
using ToeicSpace.Content.Domain.Enums;

namespace ToeicSpace.Content.Application.Interfaces.Repositories;

public interface IStudySessionRepository
{
    Task<StudySession?> GetAsync(Guid learnerId, Guid sessionId, CancellationToken ct);
    Task<StudySession?> GetForUpdateAsync(Guid learnerId, Guid sessionId, CancellationToken ct);
    /// <summary>The active session holding <paramref name="slot"/> (tracked), stale or not.</summary>
    Task<StudySession?> GetActiveBySlotForUpdateAsync(Guid learnerId, string slot, CancellationToken ct);
    /// <summary>Most recently updated active session touched after <paramref name="activeSince"/>, optionally filtered.</summary>
    Task<StudySession?> GetLatestActiveAsync(Guid learnerId, StudyMode? mode, StudySourceKind? kind, Guid? sourceId,
        DateTime activeSince, CancellationToken ct);
    void Add(StudySession session);
}
