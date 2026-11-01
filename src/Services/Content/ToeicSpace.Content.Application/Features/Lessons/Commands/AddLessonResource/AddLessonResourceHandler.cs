using ToeicSpace.Content.Application.DTOs.Lessons;
using ToeicSpace.Content.Application.Interfaces;
using ToeicSpace.Content.Application.Interfaces.Repositories;
using ToeicSpace.Content.Application.Mappings;
using ToeicSpace.Content.Application.Services;
using ToeicSpace.Content.Domain.Entities;
using ToeicSpace.Content.Domain.Enums;
using ToeicSpace.Content.Domain.Exceptions;

namespace ToeicSpace.Content.Application.Features.Lessons.Commands.AddLessonResource;

public sealed class AddLessonResourceHandler(LessonScopeService scope, ILessonRepository lessons,
    ILessonUploadRepository uploads, IUnitOfWork unitOfWork, TimeProvider clock)
    : IRequestHandler<AddLessonResourceCommand, LessonResourceDto>
{
    public async Task<LessonResourceDto> Handle(AddLessonResourceCommand request, CancellationToken ct)
    {
        var (course, _, lesson) = await scope.GetLessonAsync(request.CourseId, request.ModuleId, request.LessonId, true, ct);
        var upload = await uploads.GetConfirmedOwnedAsync(request.ObjectKey, request.AdminId,
            LessonUploadKind.Attachment, ct)
            ?? throw new ContentRuleException("unconfirmed_media", "Attachment must be confirmed by the current Admin.", 409);
        if (await lessons.IsObjectKeyUsedAsync(request.ObjectKey, ct))
            throw new ContentRuleException("media_in_use", "Attachment is already in use.", 409);
        var now = clock.GetUtcNow().UtcDateTime;
        var resource = new LessonResource
        {
            LessonId = lesson.Id, FileName = upload.FileName, ObjectKey = upload.Key,
            ContentType = upload.ContentType, FileSize = upload.SizeBytes, CreatedAt = now
        };
        course.Touch(now);
        lessons.AddResource(resource);
        await unitOfWork.SaveChangesAsync(ct);
        return resource.ToDto();
    }
}
