using ToeicSpace.Content.Application.DTOs.Courses;
using ToeicSpace.Content.Application.Interfaces;
using ToeicSpace.Content.Application.Interfaces.Repositories;
using ToeicSpace.Content.Application.Interfaces.Services;
using ToeicSpace.Content.Domain.Exceptions;

namespace ToeicSpace.Content.Application.Features.Courses.Commands.ConfirmThumbnail;

public sealed class ConfirmThumbnailsHandler(IThumbnailUploadRepository uploads, IThumbnailObjectStore storage,
    IUnitOfWork unitOfWork, TimeProvider clock) : IRequestHandler<ConfirmThumbnailsCommand, ConfirmThumbnailsResult>
{
    public async Task<ConfirmThumbnailsResult> Handle(ConfirmThumbnailsCommand request, CancellationToken ct)
    {
        var found = await uploads.GetManyForConfirmationAsync(request.Keys, request.AdminId, ct);
        var byKey = found.ToDictionary(x => x.Key, StringComparer.Ordinal);
        var items = new List<ConfirmThumbnailItemResult>(request.Keys.Count);
        var changed = false;
        var storageUnavailable = false;
        foreach (var key in request.Keys)
        {
            ct.ThrowIfCancellationRequested();
            if (!byKey.TryGetValue(key, out var upload))
            {
                items.Add(new(key, null, null, "upload_not_found", "Upload not found."));
                continue;
            }
            try
            {
                if (upload.ConfirmedAt is null)
                {
                    if (upload.ExpiresAt <= clock.GetUtcNow().UtcDateTime)
                        throw new ContentRuleException("upload_expired", "Upload confirmation window expired.", 409);
                    var obj = await storage.HeadAsync(key, ct)
                        ?? throw new ContentRuleException("upload_missing", "Uploaded thumbnail was not found.", 409);
                    if (!obj.SignatureValid || obj.SizeBytes != upload.SizeBytes || obj.SizeBytes is < 1 or > 5 * 1024 * 1024 ||
                        !string.Equals(obj.ContentType, upload.ContentType, StringComparison.OrdinalIgnoreCase))
                        throw new ContentRuleException("upload_mismatch", "Uploaded thumbnail does not match the requested size or content type.", 409);
                    upload.ConfirmedAt = clock.GetUtcNow().UtcDateTime;
                    changed = true;
                }
                items.Add(new(key, upload.ContentType, upload.SizeBytes, null, null));
            }
            catch (ContentRuleException ex)
            {
                storageUnavailable |= ex.StatusCode == 503;
                items.Add(new(key, null, null, ex.Code, ex.Message));
            }
        }
        if (storageUnavailable)
            throw new ContentRuleException("storage_unavailable", "Thumbnail storage is temporarily unavailable; retry the confirmation request.", 503);
        if (changed) await unitOfWork.SaveChangesAsync(ct);
        var successes = items.Count(x => x.Code is null);
        return new ConfirmThumbnailsResult(items.Count, successes, items.Count - successes, items);
    }
}
