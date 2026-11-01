using Microsoft.EntityFrameworkCore;
using ToeicSpace.Content.Application.Interfaces.Repositories;
using ToeicSpace.Content.Domain.Entities;

namespace ToeicSpace.Content.Infrastructure.Persistence.Repositories;

public sealed class ThumbnailUploadRepository(ContentDbContext db) : IThumbnailUploadRepository
{
    public Task<ThumbnailUpload?> GetAsync(string key, CancellationToken ct) =>
        PersistenceExceptionTranslator.ExecuteAsync(() => db.ThumbnailUploads.FirstOrDefaultAsync(x => x.Key == key, ct));
    public Task<ThumbnailUpload?> GetForConfirmationAsync(string key, Guid requestedBy, CancellationToken ct) =>
        PersistenceExceptionTranslator.ExecuteAsync(() => db.ThumbnailUploads.FirstOrDefaultAsync(
            x => x.Key == key && x.RequestedBy == requestedBy, ct));
    public async Task<IReadOnlyList<ThumbnailUpload>> GetManyForConfirmationAsync(IReadOnlyCollection<string> keys,
        Guid requestedBy, CancellationToken ct) =>
        await PersistenceExceptionTranslator.ExecuteAsync(() => db.ThumbnailUploads.Where(
            x => keys.Contains(x.Key) && x.RequestedBy == requestedBy).ToListAsync(ct));
    public Task<bool> IsReferencedByCourseAsync(string key, CancellationToken ct) =>
        PersistenceExceptionTranslator.ExecuteAsync(() => db.Courses.AnyAsync(x => x.ThumbnailKey == key, ct));
    public Task<bool> IsConfirmedByAsync(string key, Guid requestedBy, CancellationToken ct) =>
        PersistenceExceptionTranslator.ExecuteAsync(() => db.ThumbnailUploads.AnyAsync(
            x => x.Key == key && x.RequestedBy == requestedBy && x.ConfirmedAt != null, ct));
    public async Task<IReadOnlyList<ThumbnailUpload>> GetExpiredPendingAsync(DateTime cutoff, int limit, CancellationToken ct) =>
        await PersistenceExceptionTranslator.ExecuteAsync(() => db.ThumbnailUploads
            .Where(x => x.ConfirmedAt == null && x.ExpiresAt < cutoff)
            .OrderBy(x => x.ExpiresAt).Take(limit).ToListAsync(ct));
    public void Add(ThumbnailUpload upload) => db.ThumbnailUploads.Add(upload);
    public void Remove(ThumbnailUpload upload) => db.ThumbnailUploads.Remove(upload);
}
