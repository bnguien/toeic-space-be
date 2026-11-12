using ToeicSpace.Content.Application.Common.Pagination;
using ToeicSpace.Content.Application.DTOs.Courses;
using ToeicSpace.Content.Application.Interfaces.Repositories;
using ToeicSpace.Content.Application.Interfaces.Services;

namespace ToeicSpace.Content.Application.Features.Courses.Queries.BrowsePublicCourses;

public sealed class BrowsePublicCoursesHandler(IPublicCourseRepository courses, ILessonMediaStore media,
    ICoursePromotionService promotions)
    : IRequestHandler<BrowsePublicCoursesQuery, PagedResult<PublicCourseSummaryDto>>
{
    public async Task<PagedResult<PublicCourseSummaryDto>> Handle(BrowsePublicCoursesQuery request, CancellationToken ct)
    {
        var page = await courses.BrowseAsync(request.Search, request.ExamType, request.Level,
            request.TargetScore, request.Page, request.PageSize, ct);
        var remaining = await courses.GetRemainingSeatsAsync(page.Items.Where(x => x.Capacity.HasValue)
            .Select(x => x.Id).ToArray(), ct);
        var prices = await promotions.PricesAsync(page.Items.Select(x => x.Id).ToArray(), ct);
        var items = new List<PublicCourseSummaryDto>(page.Items.Count);
        foreach (var course in page.Items)
            items.Add(new(course.Id, course.Title, course.Slug, course.ShortDescription,
                course.ThumbnailKey is null ? null : await media.PresignReadAsync(course.ThumbnailKey, TimeSpan.FromMinutes(5), ct),
                course.ExamType, course.Level, course.TargetScore, course.Price, course.Capacity, "VND",
                course.Capacity.HasValue ? remaining.GetValueOrDefault(course.Id, course.Capacity.Value) : null,
                prices.GetValueOrDefault(course.Id)));
        return new PagedResult<PublicCourseSummaryDto>(items, page.Page, page.PageSize, page.TotalCount);
    }
}
