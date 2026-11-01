using ToeicSpace.Content.Domain.Entities;
using ToeicSpace.Content.Domain.Enums;

namespace ToeicSpace.Content.Application.Interfaces.Repositories;

public interface ILessonUploadRepository
{
    Task<LessonUpload?> GetOwnedAsync(string key, Guid adminId, CancellationToken ct);
    Task<LessonUpload?> GetConfirmedOwnedAsync(string key, Guid adminId, LessonUploadKind kind, CancellationToken ct);
    Task<IReadOnlyList<LessonUpload>> GetManyConfirmedOwnedAsync(IReadOnlyCollection<string> keys,
        Guid adminId, LessonUploadKind kind, CancellationToken ct);
    Task<IReadOnlyList<LessonUpload>> GetCleanupCandidatesAsync(DateTime cutoff, int limit, CancellationToken ct);
    void Add(LessonUpload upload);
    void Remove(LessonUpload upload);
}
