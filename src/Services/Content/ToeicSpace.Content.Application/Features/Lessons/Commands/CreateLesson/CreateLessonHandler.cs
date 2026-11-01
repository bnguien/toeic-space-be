using ToeicSpace.Content.Application.DTOs.Lessons;
using ToeicSpace.Content.Application.Interfaces;
using ToeicSpace.Content.Application.Interfaces.Repositories;
using ToeicSpace.Content.Application.Mappings;
using ToeicSpace.Content.Application.Services;
using ToeicSpace.Content.Domain.Entities;
using ToeicSpace.Content.Domain.Enums;
using ToeicSpace.Content.Domain.Exceptions;

namespace ToeicSpace.Content.Application.Features.Lessons.Commands.CreateLesson;

public sealed class CreateLessonHandler(LessonScopeService scope, ILessonRepository lessons,
    ILessonUploadRepository uploads, IUnitOfWork unitOfWork, TimeProvider clock)
    : IRequestHandler<CreateLessonCommand, LessonDto>
{
    public async Task<LessonDto> Handle(CreateLessonCommand request, CancellationToken ct)
    {
        await using var tx = await unitOfWork.BeginTransactionAsync(ct);
        var (course, module) = await scope.GetModuleAsync(request.CourseId, request.ModuleId, true, ct);
        var existing = await lessons.ListAsync(module.Id, tracking: false, ct);
        var position = request.SortOrder ?? existing.Count + 1;
        if (position < 1 || position > existing.Count + 1)
            throw new ContentRuleException("invalid_sort_order", "Sort order must be within the current lesson range.");
        var videoKeys = request.VideoKeys ?? [];
        if (videoKeys.Count > 10) throw new ContentRuleException("invalid_media", "A lesson can contain at most 10 videos.");
        var confirmedVideos = videoKeys.Count == 0 ? [] : await uploads.GetManyConfirmedOwnedAsync(
            videoKeys, request.AdminId, LessonUploadKind.Video, ct);
        if (confirmedVideos.Count != videoKeys.Count ||
            (await Task.WhenAll(videoKeys.Select(key => lessons.IsObjectKeyUsedAsync(key, ct)))).Any(x => x))
            throw new ContentRuleException("unconfirmed_media", "Every video must be confirmed by the current Admin and unused.", 409);
        var videosByKey = confirmedVideos.ToDictionary(x => x.Key, StringComparer.Ordinal);
        var now = clock.GetUtcNow().UtcDateTime;
        var lesson = new Lesson { ModuleId = module.Id, SortOrder = position, CreatedAt = now };
        lesson.SetMetadata(request.Title, request.Description, request.TextContent,
            request.DurationSeconds, request.IsFreePreview, now);
        lesson.Videos = videoKeys.Select((key, index) => videosByKey[key]).Select((x, index) => new LessonVideo
        {
            LessonId = lesson.Id, ObjectKey = x.Key, FileName = x.FileName,
            ContentType = x.ContentType, FileSize = x.SizeBytes, SortOrder = index + 1, CreatedAt = now
        }).ToList();
        course.Touch(now);
        await unitOfWork.SaveChangesAsync(ct);
        if (position <= existing.Count)
        {
            var targets = existing.ToDictionary(x => x.Id,
                x => x.SortOrder >= position ? x.SortOrder + 1 : x.SortOrder);
            await LessonOrdering.ReindexAsync(lessons, unitOfWork, module.Id, targets, existing.Count + 1, now, ct);
        }
        lessons.Add(lesson);
        await unitOfWork.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return lesson.ToDto();
    }
}
