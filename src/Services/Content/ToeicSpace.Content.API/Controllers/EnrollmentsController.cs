using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ToeicSpace.BuildingBlocks.Security.Jwt;
using ToeicSpace.Content.Application.Common.Pagination;
using ToeicSpace.Content.Application.DTOs.Courses;
using ToeicSpace.Content.Application.Interfaces.Services;
using ToeicSpace.Content.Domain.Entities;
using ToeicSpace.Content.Domain.Exceptions;

namespace ToeicSpace.Content.API.Controllers;

[ApiController]
[Route("api/v1/enrollments")]
[Authorize(Roles = "User")]
public sealed class EnrollmentsController(ICourseEnrollmentService enrollments) : ControllerBase
{
    private Guid UserId => Guid.TryParse(User.FindFirst(AccessTokenDefaults.SubjectClaim)?.Value, out var id)
        ? id : throw new ContentRuleException("invalid_subject", "Invalid user identity.", 401);

    [HttpPost("{courseId:guid}")]
    public Task<CourseEnrollment> FreeEnroll(Guid courseId, CancellationToken ct) =>
        enrollments.FreeEnrollAsync(courseId, UserId, ct);

    [HttpGet("{courseId:guid}")]
    public Task<CourseEnrollment?> Status(Guid courseId, CancellationToken ct) =>
        enrollments.StatusAsync(courseId, UserId, ct);

    [HttpGet("me")]
    public Task<PagedResult<MyCourseDto>> Mine([FromQuery] string? search, [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20, CancellationToken ct = default) =>
        enrollments.MineAsync(UserId, search, page, pageSize, ct);

    [HttpGet("{courseId:guid}/lessons/{lessonId:guid}")]
    public Task<PublicLessonPreviewDto> Lesson(Guid courseId, Guid lessonId, CancellationToken ct) =>
        enrollments.LessonAsync(courseId, lessonId, UserId, ct);
}
