using ToeicSpace.Content.Application.DTOs.Courses;
using ToeicSpace.Content.Application.Interfaces.Repositories;
using ToeicSpace.Content.Application.Interfaces.Services;
using ToeicSpace.Content.Domain.Enums;
using ToeicSpace.Content.Domain.Exceptions;

namespace ToeicSpace.Content.Application.Features.Courses.Queries.GetPublicCourse;

public sealed class GetPublicCourseHandler(IPublicCourseRepository courses, ILessonMediaStore media)
    : IRequestHandler<GetPublicCourseQuery, PublicCourseDetailDto>
{
    public async Task<PublicCourseDetailDto> Handle(GetPublicCourseQuery request, CancellationToken ct)
    {
        var course = await courses.GetPublishedAsync(request.Id, ct) ?? throw ContentRuleException.NotFound();
        var modules = await courses.GetCurriculumAsync(course.Id, ct);
        var thumbnailUrl = course.ThumbnailKey is null ? null
            : await media.PresignReadAsync(course.ThumbnailKey, TimeSpan.FromMinutes(5), ct);
        return new PublicCourseDetailDto(course.Id, course.Title, course.Slug, thumbnailUrl,
            course.ShortDescription, course.Description, course.ExamType, course.Level, course.TargetScore,
            course.Skills.ToArray(), course.Objectives.ToArray(), course.Prerequisites.ToArray(),
            modules.Select(module => new PublicModuleDto(module.Id, module.Title, module.Description, module.SortOrder,
                module.Lessons.Where(x => x.Status == LessonStatus.Published)
                    .OrderBy(x => x.SortOrder).ThenBy(x => x.Id)
                    .Select(x => new PublicLessonSummaryDto(x.Id, x.Title, x.Description, x.DurationSeconds,
                        x.SortOrder, x.IsFreePreview)).ToArray())).ToArray());
    }
}
