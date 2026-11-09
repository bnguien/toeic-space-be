using ToeicSpace.Content.Application.DTOs.Lessons;

namespace ToeicSpace.Content.Application.Features.Lessons.Commands.ConfirmLessonUpload;

public sealed record ConfirmLessonUploadCommand(string Key, Guid AdminId) : IRequest<ConfirmedLessonUploadDto>;
public sealed record ConfirmLessonUploadsCommand(IReadOnlyList<string> Keys, Guid AdminId)
    : IRequest<ConfirmLessonUploadsResult>;
