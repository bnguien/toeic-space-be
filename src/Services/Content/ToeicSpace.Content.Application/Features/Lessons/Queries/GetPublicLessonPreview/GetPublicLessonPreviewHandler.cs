using ToeicSpace.Content.Application.DTOs.Courses;
using ToeicSpace.Content.Application.Interfaces.Repositories;
using ToeicSpace.Content.Application.Interfaces.Services;
using ToeicSpace.Content.Domain.Exceptions;

namespace ToeicSpace.Content.Application.Features.Lessons.Queries.GetPublicLessonPreview;

public sealed class GetPublicLessonPreviewHandler(IPublicCourseRepository courses, ILessonMediaStore media)
    : IRequestHandler<GetPublicLessonPreviewQuery, PublicLessonPreviewDto>
{
    public async Task<PublicLessonPreviewDto> Handle(GetPublicLessonPreviewQuery request, CancellationToken ct)
    {
        // The repository checks course, parent module, lesson status and free-preview entitlement
        // before any media key is sent to storage for signing.
        var lesson = await courses.GetPreviewAsync(request.CourseId, request.LessonId, ct)
            ?? throw ContentRuleException.NotFound();
        var lifetime = TimeSpan.FromMinutes(5);
        var videos = new List<PublicVideoDto>();
        foreach (var video in lesson.Videos.OrderBy(x => x.SortOrder).ThenBy(x => x.Id))
            videos.Add(new(video.Id, video.FileName, video.ContentType, video.FileSize,
                video.DurationSeconds, video.SortOrder, await media.PresignReadAsync(video.ObjectKey, lifetime, ct)));
        var attachments = new List<PublicAttachmentDto>();
        foreach (var resource in lesson.Resources.OrderBy(x => x.CreatedAt).ThenBy(x => x.Id))
            attachments.Add(new(resource.Id, resource.FileName, resource.ContentType,
                resource.FileSize, await media.PresignReadAsync(resource.ObjectKey, lifetime, ct)));
        return new PublicLessonPreviewDto(lesson.Id, lesson.Title, lesson.TextContent, videos, attachments);
    }
}
