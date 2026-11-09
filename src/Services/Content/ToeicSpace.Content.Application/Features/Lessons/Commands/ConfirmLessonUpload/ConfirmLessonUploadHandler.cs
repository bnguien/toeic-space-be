using ToeicSpace.Content.Application.DTOs.Lessons;
using ToeicSpace.Content.Application.Interfaces;
using ToeicSpace.Content.Application.Interfaces.Repositories;
using ToeicSpace.Content.Application.Interfaces.Services;
using ToeicSpace.Content.Domain.Exceptions;

namespace ToeicSpace.Content.Application.Features.Lessons.Commands.ConfirmLessonUpload;

public sealed class ConfirmLessonUploadHandler(ILessonUploadRepository uploads, ILessonMediaStore storage,
    IUnitOfWork unitOfWork, TimeProvider clock)
    : IRequestHandler<ConfirmLessonUploadCommand, ConfirmedLessonUploadDto>
{
    public async Task<ConfirmedLessonUploadDto> Handle(ConfirmLessonUploadCommand request, CancellationToken ct)
    {
        var upload = await uploads.GetOwnedAsync(request.Key, request.AdminId, ct)
            ?? throw new ContentRuleException("upload_not_found", "Upload not found.", 404);
        if (upload.ConfirmedAt is null)
        {
            if (upload.ExpiresAt <= clock.GetUtcNow().UtcDateTime)
                throw new ContentRuleException("upload_expired", "Upload confirmation window expired.", 409);
            var obj = await storage.HeadAsync(upload.Key, ct)
                ?? throw new ContentRuleException("upload_missing", "Uploaded media not found.", 409);
            if (!obj.SignatureValid || obj.SizeBytes != upload.SizeBytes ||
                !string.Equals(obj.ContentType, upload.ContentType, StringComparison.OrdinalIgnoreCase))
                throw new ContentRuleException("upload_mismatch", "Uploaded media differs from the requested type or size.", 409);
            upload.ConfirmedAt = clock.GetUtcNow().UtcDateTime;
            await unitOfWork.SaveChangesAsync(ct);
        }
        return new ConfirmedLessonUploadDto(upload.Key, upload.Kind, upload.FileName,
            upload.ContentType, upload.SizeBytes);
    }
}
