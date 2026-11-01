using ToeicSpace.Content.Application.DTOs.Lessons;
using ToeicSpace.Content.Application.Interfaces;
using ToeicSpace.Content.Application.Interfaces.Repositories;
using ToeicSpace.Content.Application.Interfaces.Services;
using ToeicSpace.Content.Domain.Entities;
using ToeicSpace.Content.Domain.Enums;
using ToeicSpace.Content.Domain.Exceptions;

namespace ToeicSpace.Content.Application.Features.Lessons.Commands.PresignLessonUpload;

public sealed class PresignLessonUploadHandler(ILessonUploadRepository uploads, ILessonMediaStore storage,
    IUnitOfWork unitOfWork, TimeProvider clock)
    : IRequestHandler<PresignLessonUploadCommand, PresignedLessonUploadDto>
{
    public async Task<PresignedLessonUploadDto> Handle(PresignLessonUploadCommand request, CancellationToken ct)
    {
        var ext = (request.Kind, request.ContentType) switch
        {
            (LessonUploadKind.Video, "video/mp4") => "mp4",
            (LessonUploadKind.Attachment, "application/pdf") => "pdf",
            (LessonUploadKind.Attachment, "image/jpeg") => "jpg",
            (LessonUploadKind.Attachment, "image/png") => "png",
            (LessonUploadKind.Attachment, "image/webp") => "webp",
            _ => throw new ContentRuleException("invalid_media", "Unsupported media type for this upload kind.")
        };
        var max = request.Kind == LessonUploadKind.Video ? 500L * 1024 * 1024 : 20L * 1024 * 1024;
        var fileName = Path.GetFileName(request.FileName?.Replace('\\', '/') ?? string.Empty);
        if (request.SizeBytes is < 1 || request.SizeBytes > max || fileName.Length is < 1 or > 255 ||
            fileName.Any(char.IsControl))
            throw new ContentRuleException("invalid_media", "Media file name or size is invalid.");
        var now = clock.GetUtcNow().UtcDateTime;
        var upload = new LessonUpload
        {
            Key = $"courses/lessons/{request.AdminId:N}/{Guid.NewGuid():N}.{ext}",
            RequestedBy = request.AdminId, Kind = request.Kind, FileName = fileName,
            ContentType = request.ContentType, SizeBytes = request.SizeBytes,
            CreatedAt = now, ExpiresAt = now.AddMinutes(10)
        };
        uploads.Add(upload);
        await unitOfWork.SaveChangesAsync(ct);
        var url = await storage.PresignUploadAsync(upload.Key, upload.ContentType, TimeSpan.FromMinutes(10), ct);
        return new PresignedLessonUploadDto(upload.Key, url, upload.Kind, upload.ContentType, upload.ExpiresAt);
    }
}
