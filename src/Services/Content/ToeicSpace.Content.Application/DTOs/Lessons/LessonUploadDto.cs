using ToeicSpace.Content.Domain.Enums;

namespace ToeicSpace.Content.Application.DTOs.Lessons;

public sealed record PresignedLessonUploadDto(string Key, string UploadUrl, LessonUploadKind Kind,
    string ContentType, DateTime ExpiresAt);
public sealed record ConfirmedLessonUploadDto(string Key, LessonUploadKind Kind,
    string FileName, string ContentType, long SizeBytes);
public sealed record LessonUploadRequest(LessonUploadKind Kind, string FileName, string ContentType, long SizeBytes);
public sealed record PresignLessonUploadsResult(int TotalCount, int PresignedCount, int FailedCount,
    IReadOnlyList<PresignLessonUploadItemResult> Items);
public sealed record PresignLessonUploadItemResult(int Index, PresignedLessonUploadDto? Upload,
    string? Code, string? Message);
public sealed record ConfirmLessonUploadsResult(int TotalCount, int ConfirmedCount, int FailedCount,
    IReadOnlyList<ConfirmLessonUploadItemResult> Items);
public sealed record ConfirmLessonUploadItemResult(string Key, ConfirmedLessonUploadDto? Upload,
    string? Code, string? Message);
