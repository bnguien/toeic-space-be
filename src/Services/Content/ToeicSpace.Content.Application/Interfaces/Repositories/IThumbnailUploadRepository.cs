using ToeicSpace.Content.Domain.Entities;

namespace ToeicSpace.Content.Application.Interfaces.Repositories;

public interface IThumbnailUploadRepository
{
    Task<ThumbnailUpload?> GetAsync(string key, CancellationToken ct);
    Task<ThumbnailUpload?> GetForConfirmationAsync(string key, Guid requestedBy, CancellationToken ct);
    Task<IReadOnlyList<ThumbnailUpload>> GetManyForConfirmationAsync(IReadOnlyCollection<string> keys,
        Guid requestedBy, CancellationToken ct);
    Task<bool> IsReferencedByCourseAsync(string key, CancellationToken ct);
    Task<bool> IsConfirmedByAsync(string key, Guid requestedBy, CancellationToken ct);
    Task<IReadOnlyList<ThumbnailUpload>> GetExpiredPendingAsync(DateTime cutoff, int limit, CancellationToken ct);
    void Add(ThumbnailUpload upload);
    void Remove(ThumbnailUpload upload);
}
