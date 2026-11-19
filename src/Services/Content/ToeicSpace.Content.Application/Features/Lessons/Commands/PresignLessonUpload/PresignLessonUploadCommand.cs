using ToeicSpace.Content.Application.DTOs.Lessons;
using ToeicSpace.Content.Domain.Enums;

namespace ToeicSpace.Content.Application.Features.Lessons.Commands.PresignLessonUpload;

public sealed record PresignLessonUploadCommand(LessonUploadKind Kind, string FileName,
    string ContentType, long SizeBytes, Guid AdminId) : IRequest<PresignedLessonUploadDto>;
public sealed record PresignLessonUploadsCommand(IReadOnlyList<LessonUploadRequest> Files, Guid AdminId)
    : IRequest<PresignLessonUploadsResult>;
