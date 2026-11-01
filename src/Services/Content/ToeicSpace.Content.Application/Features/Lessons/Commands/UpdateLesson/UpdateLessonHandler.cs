using ToeicSpace.Content.Application.DTOs.Lessons;
using ToeicSpace.Content.Application.Interfaces;
using ToeicSpace.Content.Application.Interfaces.Repositories;
using ToeicSpace.Content.Application.Mappings;
using ToeicSpace.Content.Application.Services;
using ToeicSpace.Content.Domain.Entities;
using ToeicSpace.Content.Domain.Enums;
using ToeicSpace.Content.Domain.Exceptions;

namespace ToeicSpace.Content.Application.Features.Lessons.Commands.UpdateLesson;

public sealed class UpdateLessonHandler(LessonScopeService scope, ILessonUploadRepository uploads,
    ILessonRepository lessons, IUnitOfWork unitOfWork, TimeProvider clock) : IRequestHandler<UpdateLessonCommand, LessonDto>
{
    public async Task<LessonDto> Handle(UpdateLessonCommand request, CancellationToken ct)
    {
        var (course, _, lesson) = await scope.GetLessonAsync(request.CourseId, request.ModuleId, request.LessonId, true, ct);
        var videoKeys = request.VideoKeys ?? [];
        if (videoKeys.Count > 10 || videoKeys.Distinct(StringComparer.Ordinal).Count() != videoKeys.Count)
            throw new ContentRuleException("invalid_media", "Video keys must be unique and contain at most 10 items.");
        var previousVideos = lesson.Videos.ToDictionary(x => x.ObjectKey, StringComparer.Ordinal);
        var previousKeys = previousVideos.Keys.ToHashSet(StringComparer.Ordinal);
        var newKeys = videoKeys.Where(x => !previousKeys.Contains(x)).ToArray();
        var confirmed = newKeys.Length == 0 ? [] : await uploads.GetManyConfirmedOwnedAsync(
            newKeys, request.AdminId, LessonUploadKind.Video, ct);
        if (confirmed.Count != newKeys.Length ||
            (await Task.WhenAll(newKeys.Select(key => lessons.IsObjectKeyUsedAsync(key, ct)))).Any(x => x))
            throw new ContentRuleException("unconfirmed_media", "Every new video must be confirmed by the current Admin and unused.", 409);
        var uploadsByKey = confirmed.ToDictionary(x => x.Key, StringComparer.Ordinal);
        var now = clock.GetUtcNow().UtcDateTime;
        lesson.SetMetadata(request.Title, request.Description, request.TextContent,
            request.DurationSeconds, request.IsFreePreview, now);
        foreach (var (key, oldVideo) in previousVideos)
        {
            if (!videoKeys.Contains(key, StringComparer.Ordinal))
            {
                lessons.RemoveVideo(oldVideo);
                lesson.Videos.Remove(oldVideo);
            }
        }
        for (var index = 0; index < videoKeys.Count; index++)
        {
            var key = videoKeys[index];
            if (previousVideos.TryGetValue(key, out var existing))
            {
                existing.SortOrder = index + 1;
                continue;
            }
            var upload = uploadsByKey[key];
            var added = new LessonVideo { LessonId = lesson.Id, ObjectKey = upload.Key, FileName = upload.FileName,
                ContentType = upload.ContentType, FileSize = upload.SizeBytes, SortOrder = index + 1, CreatedAt = now };
            lessons.AddVideo(added);
        }
        if (lesson.Status == LessonStatus.Published && string.IsNullOrWhiteSpace(lesson.TextContent)
            && lesson.Videos.Count == 0 && lesson.Resources.Count == 0)
            throw new ContentRuleException("publish_requirements", "Published lesson must retain text, video or attachments.", 409);
        course.Touch(now);
        await unitOfWork.SaveChangesAsync(ct);
        return lesson.ToDto();
    }
}
