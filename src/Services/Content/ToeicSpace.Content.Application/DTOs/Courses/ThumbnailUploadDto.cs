namespace ToeicSpace.Content.Application.DTOs.Courses;

public sealed record PresignedThumbnailDto(string Key, string UploadUrl, string ContentType, DateTime ExpiresAt);
public sealed record ConfirmedThumbnailDto(string Key, string ContentType, long SizeBytes);
public sealed record ConfirmThumbnailItemResult(string Key, string? ContentType, long? SizeBytes,
    string? Code, string? Message);
public sealed record ConfirmThumbnailsResult(int TotalCount, int ConfirmedCount, int FailedCount,
    IReadOnlyList<ConfirmThumbnailItemResult> Items);
