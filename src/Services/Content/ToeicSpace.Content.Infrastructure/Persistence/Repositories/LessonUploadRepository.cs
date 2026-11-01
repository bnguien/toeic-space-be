using Microsoft.EntityFrameworkCore;
using ToeicSpace.Content.Application.Interfaces.Repositories;
using ToeicSpace.Content.Domain.Entities;
using ToeicSpace.Content.Domain.Enums;

namespace ToeicSpace.Content.Infrastructure.Persistence.Repositories;

public sealed class LessonUploadRepository(ContentDbContext db) : ILessonUploadRepository
{
    public Task<LessonUpload?> GetOwnedAsync(string key, Guid adminId, CancellationToken ct) =>
        PersistenceExceptionTranslator.ExecuteAsync(() => db.LessonUploads
            .FirstOrDefaultAsync(x => x.Key == key && x.RequestedBy == adminId, ct));
    public Task<LessonUpload?> GetConfirmedOwnedAsync(string key, Guid adminId, LessonUploadKind kind, CancellationToken ct) =>
        PersistenceExceptionTranslator.ExecuteAsync(() => db.LessonUploads.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Key == key && x.RequestedBy == adminId
                && x.Kind == kind && x.ConfirmedAt != null, ct));
    public async Task<IReadOnlyList<LessonUpload>> GetManyConfirmedOwnedAsync(IReadOnlyCollection<string> keys,
        Guid adminId, LessonUploadKind kind, CancellationToken ct) =>
        await PersistenceExceptionTranslator.ExecuteAsync(() => db.LessonUploads.Where(x => keys.Contains(x.Key)
            && x.RequestedBy == adminId && x.Kind == kind && x.ConfirmedAt != null).ToListAsync(ct));
    public async Task<IReadOnlyList<LessonUpload>> GetCleanupCandidatesAsync(DateTime cutoff, int limit, CancellationToken ct) =>
        await PersistenceExceptionTranslator.ExecuteAsync(() => db.LessonUploads.Where(x =>
                ((x.ConfirmedAt == null && x.ExpiresAt < cutoff) ||
                (x.ConfirmedAt != null && x.ConfirmedAt < cutoff)) &&
                !db.LessonVideos.Any(l => l.ObjectKey == x.Key) &&
                !db.LessonResources.Any(r => r.ObjectKey == x.Key))
            .OrderBy(x => x.CreatedAt).Take(limit).ToListAsync(ct));
    public void Add(LessonUpload upload) => db.LessonUploads.Add(upload);
    public void Remove(LessonUpload upload) => db.LessonUploads.Remove(upload);
}
