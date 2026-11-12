using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ToeicSpace.Content.Application.Common.Pagination;
using ToeicSpace.Content.Application.DTOs.Courses;
using ToeicSpace.Content.Application.Features.Courses.Commands.ArchiveCourse;
using ToeicSpace.Content.Application.Features.Courses.Commands.CreateCourse;
using ToeicSpace.Content.Application.Features.Courses.Commands.PublishCourse;
using ToeicSpace.Content.Application.Features.Courses.Commands.RestoreCourse;
using ToeicSpace.Content.Application.Features.Courses.Commands.UpdateCourse;
using ToeicSpace.Content.Application.Features.Courses.Commands.UpdateCoursePricing;
using ToeicSpace.Content.Application.Features.Courses.Queries.GetCourseById;
using ToeicSpace.Content.Application.Features.Courses.Queries.GetCourses;
using ToeicSpace.BuildingBlocks.Security.Jwt;
using ToeicSpace.Content.Domain.Exceptions;
using ToeicSpace.Content.Domain.Enums;

namespace ToeicSpace.Content.API.Controllers;

[ApiController]
[Route("api/v1/courses")]
[Authorize(Policy = "Admin")]
public sealed class CoursesController(ISender sender) : ControllerBase
{
    private Guid AdminId => Guid.TryParse(User.FindFirst(AccessTokenDefaults.SubjectClaim)?.Value, out var id)
        ? id : throw new ContentRuleException("invalid_subject", "Invalid user identity.", 401);

    [HttpPost]
    public async Task<ActionResult<CourseDto>> Create([FromBody] CreateCourseCommand command, CancellationToken ct)
    {
        command = command with { AdminId = AdminId };
        var course = await sender.Send(command, ct);
        return CreatedAtAction(nameof(Get), new { id = course.Id }, course);
    }

    [HttpGet]
    public Task<PagedResult<CourseDto>> List([FromQuery] string? search, [FromQuery] CourseStatus? status,
        [FromQuery] CourseLevel? level, [FromQuery] CourseExamType? examType,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default) =>
        sender.Send(new GetCoursesQuery(search, status, level, examType, page, pageSize), ct);

    [HttpGet("{id:guid}")]
    public Task<CourseDto> Get(Guid id, CancellationToken ct) => sender.Send(new GetCourseByIdQuery(id), ct);

    [HttpPut("{id:guid}")]
    public Task<CourseDto> Update(Guid id, UpdateCourseCommand command, CancellationToken ct)
    {
        command.CourseId = id;
        command.AdminId = AdminId;
        return sender.Send(command, ct);
    }

    [HttpPut("{id:guid}/pricing")]
    public Task<CourseDto> UpdatePricing(Guid id, [FromBody] CoursePricingRequest body, CancellationToken ct) =>
        sender.Send(new UpdateCoursePricingCommand(id, body.Price, body.Capacity), ct);

    public sealed record CoursePricingRequest(decimal Price, int? Capacity);

    [HttpPost("{id:guid}/publish")]
    public Task<CourseDto> Publish(Guid id, CancellationToken ct) => sender.Send(new PublishCourseCommand(id), ct);

    [HttpPost("{id:guid}/archive")]
    public Task<CourseDto> Archive(Guid id, CancellationToken ct) => sender.Send(new ArchiveCourseCommand(id), ct);

    [HttpPost("{id:guid}/restore")]
    public Task<CourseDto> Restore(Guid id, CancellationToken ct) => sender.Send(new RestoreCourseCommand(id), ct);
}
