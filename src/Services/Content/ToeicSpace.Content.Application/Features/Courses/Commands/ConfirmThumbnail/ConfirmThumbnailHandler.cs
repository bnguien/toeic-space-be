using ToeicSpace.Content.Application.DTOs.Courses;
using ToeicSpace.Content.Application.Interfaces;
using ToeicSpace.Content.Application.Interfaces.Repositories;
using ToeicSpace.Content.Application.Interfaces.Services;
using ToeicSpace.Content.Domain.Exceptions;

namespace ToeicSpace.Content.Application.Features.Courses.Commands.ConfirmThumbnail;

public sealed class ConfirmThumbnailHandler(IThumbnailUploadRepository uploads, IThumbnailObjectStore storage,
    IUnitOfWork unitOfWork, TimeProvider clock)
    : IRequestHandler<ConfirmThumbnailCommand, ConfirmedThumbnailDto>
{
    public async Task<ConfirmedThumbnailDto> Handle(ConfirmThumbnailCommand request, CancellationToken ct)
    {
        var upload = await uploads.GetForConfirmationAsync(request.Key, request.AdminId, ct);
        if (upload is null)
            throw new ContentRuleException("upload_not_found", "Upload not found.", 404);
        if (upload.ConfirmedAt is null)
        {
            if (upload.ExpiresAt <= clock.GetUtcNow().UtcDateTime)
                throw new ContentRuleException("upload_expired", "Upload confirmation window expired.", 409);
            var objectInfo = await storage.HeadAsync(upload.Key, ct)
                ?? throw new ContentRuleException("upload_missing", "Uploaded thumbnail was not found.", 409);
            if (!objectInfo.SignatureValid || objectInfo.SizeBytes != upload.SizeBytes || objectInfo.SizeBytes is < 1 or > 5 * 1024 * 1024 ||
                !string.Equals(objectInfo.ContentType, upload.ContentType, StringComparison.OrdinalIgnoreCase))
                throw new ContentRuleException("upload_mismatch", "Uploaded thumbnail does not match the requested size or content type.", 409);
            upload.ConfirmedAt = clock.GetUtcNow().UtcDateTime;
            await unitOfWork.SaveChangesAsync(ct);
        }
        return new ConfirmedThumbnailDto(upload.Key, upload.ContentType, upload.SizeBytes);
    }
}
