using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ToeicSpace.Content.Application.Common.Pagination;
using ToeicSpace.Content.Application.DTOs.Courses;
using ToeicSpace.Content.Application.Features.Courses.Queries.BrowsePublicCourses;
using ToeicSpace.Content.Application.Features.Courses.Queries.GetPublicCourse;
using ToeicSpace.Content.Application.Features.Lessons.Queries.GetPublicLessonPreview;
using ToeicSpace.Content.Domain.Enums;

namespace ToeicSpace.Content.API.Controllers;

[ApiController]
[Route("api/v1/public/courses")]
[AllowAnonymous]
public sealed class PublicCoursesController(ISender sender) : ControllerBase
{
    [HttpGet]
    public Task<PagedResult<PublicCourseSummaryDto>> Browse([FromQuery] string? search,
        [FromQuery] CourseExamType? examType, [FromQuery] CourseLevel? level, [FromQuery] int? targetScore,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default) =>
        sender.Send(new BrowsePublicCoursesQuery(search, examType, level, targetScore, page, pageSize), ct);

    [HttpGet("{id:guid}")]
    public Task<PublicCourseDetailDto> Get(Guid id, CancellationToken ct) =>
        sender.Send(new GetPublicCourseQuery(id), ct);

    [HttpGet("{courseId:guid}/lessons/{lessonId:guid}/preview")]
    public Task<PublicLessonPreviewDto> Preview(Guid courseId, Guid lessonId, CancellationToken ct) =>
        sender.Send(new GetPublicLessonPreviewQuery(courseId, lessonId), ct);
}
