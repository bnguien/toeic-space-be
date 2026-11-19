using ToeicSpace.Content.Application.DTOs.Lessons;
using ToeicSpace.Content.Application.Interfaces.Services;
using ToeicSpace.Content.Application.Mappings;
using ToeicSpace.Content.Application.Services;

namespace ToeicSpace.Content.Application.Features.Lessons.Queries.GetLessonPreview;

public sealed class GetLessonPreviewHandler(LessonScopeService scope, ILessonMediaStore storage)
    : IRequestHandler<GetLessonPreviewQuery, LessonPreviewDto>
{
    public async Task<LessonPreviewDto> Handle(GetLessonPreviewQuery request, CancellationToken ct)
    {
        var (_, _, lesson) = await scope.GetLessonAsync(request.CourseId, request.ModuleId, request.LessonId, false, ct);
        var lifetime = TimeSpan.FromMinutes(5);
        var videos = new List<LessonVideoPreviewDto>(lesson.Videos.Count);
        foreach (var video in lesson.Videos.OrderBy(x => x.SortOrder))
            videos.Add(new(video.Id, video.FileName, await storage.PresignReadAsync(video.ObjectKey, lifetime, ct), video.SortOrder));
        var resources = new List<LessonResourcePreviewDto>(lesson.Resources.Count);
        foreach (var resource in lesson.Resources.OrderBy(x => x.CreatedAt).ThenBy(x => x.Id))
            resources.Add(new(resource.Id, resource.FileName,
                await storage.PresignReadAsync(resource.ObjectKey, lifetime, ct)));
        return new LessonPreviewDto(lesson.ToDto(), videos, resources);
    }
}
