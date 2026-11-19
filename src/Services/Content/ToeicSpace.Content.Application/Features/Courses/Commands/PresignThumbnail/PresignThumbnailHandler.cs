using ToeicSpace.Content.Application.DTOs.Courses;
using ToeicSpace.Content.Application.Interfaces;
using ToeicSpace.Content.Application.Interfaces.Repositories;
using ToeicSpace.Content.Application.Interfaces.Services;
using ToeicSpace.Content.Domain.Entities;
using ToeicSpace.Content.Domain.Exceptions;

namespace ToeicSpace.Content.Application.Features.Courses.Commands.PresignThumbnail;

public sealed class PresignThumbnailHandler(IThumbnailUploadRepository uploads, IThumbnailObjectStore storage,
    IUnitOfWork unitOfWork, TimeProvider clock)
    : IRequestHandler<PresignThumbnailCommand, PresignedThumbnailDto>
{
    public async Task<PresignedThumbnailDto> Handle(PresignThumbnailCommand request, CancellationToken ct)
    {
        var extension = request.ContentType switch
        {
            "image/jpeg" => "jpg", "image/png" => "png", "image/webp" => "webp",
            _ => throw new ContentRuleException("invalid_thumbnail", "Only JPEG, PNG and WebP thumbnails are supported.")
        };
        if (request.SizeBytes is < 1 or > 5 * 1024 * 1024)
            throw new ContentRuleException("invalid_thumbnail", "Thumbnail must be between 1 byte and 5 MB.");
        var now = clock.GetUtcNow().UtcDateTime;
        var upload = new ThumbnailUpload
        {
            Key = $"courses/thumbnails/{request.AdminId:N}/{Guid.NewGuid():N}.{extension}", RequestedBy = request.AdminId,
            ContentType = request.ContentType, SizeBytes = request.SizeBytes,
            CreatedAt = now, ExpiresAt = now.AddMinutes(10)
        };
        // Persist the server-owned key before handing the upload URL to the browser.
        uploads.Add(upload);
        await unitOfWork.SaveChangesAsync(ct);
        var url = await storage.PresignAsync(upload.Key, upload.ContentType, TimeSpan.FromMinutes(10), ct);
        return new PresignedThumbnailDto(upload.Key, url, upload.ContentType, upload.ExpiresAt);
    }
}
