using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ToeicSpace.BuildingBlocks.Security.Jwt;
using ToeicSpace.Content.Application.DTOs.Lessons;
using ToeicSpace.Content.Application.Features.Lessons.Commands.AddLessonResource;
using ToeicSpace.Content.Application.Features.Lessons.Commands.CreateLesson;
using ToeicSpace.Content.Application.Features.Lessons.Commands.DeleteLesson;
using ToeicSpace.Content.Application.Features.Lessons.Commands.PublishLesson;
using ToeicSpace.Content.Application.Features.Lessons.Commands.RemoveLessonResource;
using ToeicSpace.Content.Application.Features.Lessons.Commands.ReorderLessons;
using ToeicSpace.Content.Application.Features.Lessons.Commands.SetLessonFreePreview;
using ToeicSpace.Content.Application.Features.Lessons.Commands.UnpublishLesson;
using ToeicSpace.Content.Application.Features.Lessons.Commands.UpdateLesson;
using ToeicSpace.Content.Application.Features.Lessons.Queries.GetLessonDetail;
using ToeicSpace.Content.Application.Features.Lessons.Queries.GetLessonPreview;
using ToeicSpace.Content.Application.Features.Lessons.Queries.GetLessons;
using ToeicSpace.Content.Domain.Exceptions;

namespace ToeicSpace.Content.API.Controllers;

[ApiController]
[Route("api/v1/courses/{courseId:guid}/modules/{moduleId:guid}/lessons")]
[Authorize(Policy = "Admin")]
public sealed class CourseLessonsController(ISender sender) : ControllerBase
{
    private Guid AdminId => Guid.TryParse(User.FindFirst(AccessTokenDefaults.SubjectClaim)?.Value, out var id)
        ? id : throw new ContentRuleException("invalid_subject", "Invalid user identity.", 401);

    [HttpPost]
    public async Task<ActionResult<LessonDto>> Create(Guid courseId, Guid moduleId, CreateLessonCommand command, CancellationToken ct)
    {
        command.CourseId = courseId; command.ModuleId = moduleId; command.AdminId = AdminId;
        var result = await sender.Send(command, ct);
        return CreatedAtAction(nameof(Get), new { courseId, moduleId, lessonId = result.Id }, result);
    }

    [HttpGet]
    public Task<IReadOnlyList<LessonSummaryDto>> List(Guid courseId, Guid moduleId, CancellationToken ct) =>
        sender.Send(new GetLessonsQuery(courseId, moduleId), ct);

    [HttpGet("{lessonId:guid}")]
    public Task<LessonDto> Get(Guid courseId, Guid moduleId, Guid lessonId, CancellationToken ct) =>
        sender.Send(new GetLessonDetailQuery(courseId, moduleId, lessonId), ct);

    [HttpPut("{lessonId:guid}")]
    public Task<LessonDto> Update(Guid courseId, Guid moduleId, Guid lessonId, UpdateLessonCommand command, CancellationToken ct)
    {
        command.CourseId = courseId; command.ModuleId = moduleId; command.LessonId = lessonId; command.AdminId = AdminId;
        return sender.Send(command, ct);
    }

    [HttpDelete("{lessonId:guid}")]
    public async Task<IActionResult> Delete(Guid courseId, Guid moduleId, Guid lessonId, CancellationToken ct)
    {
        await sender.Send(new DeleteLessonCommand(courseId, moduleId, lessonId), ct);
        return NoContent();
    }

    [HttpPut("order")]
    public Task<IReadOnlyList<LessonSummaryDto>> Reorder(Guid courseId, Guid moduleId, ReorderLessonsCommand command, CancellationToken ct)
    {
        command.CourseId = courseId; command.ModuleId = moduleId;
        return sender.Send(command, ct);
    }

    [HttpPost("{lessonId:guid}/publish")]
    public Task<LessonDto> Publish(Guid courseId, Guid moduleId, Guid lessonId, CancellationToken ct) =>
        sender.Send(new PublishLessonCommand(courseId, moduleId, lessonId), ct);

    [HttpPost("{lessonId:guid}/unpublish")]
    public Task<LessonDto> Unpublish(Guid courseId, Guid moduleId, Guid lessonId, CancellationToken ct) =>
        sender.Send(new UnpublishLessonCommand(courseId, moduleId, lessonId), ct);

    [HttpPatch("{lessonId:guid}/free-preview")]
    public Task<LessonDto> SetFreePreview(Guid courseId, Guid moduleId, Guid lessonId,
        SetLessonFreePreviewCommand command, CancellationToken ct)
    {
        command.CourseId = courseId; command.ModuleId = moduleId; command.LessonId = lessonId;
        return sender.Send(command, ct);
    }

    [HttpPost("{lessonId:guid}/resources")]
    public async Task<ActionResult<LessonResourceDto>> AddResource(Guid courseId, Guid moduleId, Guid lessonId,
        AddLessonResourceCommand command, CancellationToken ct)
    {
        command.CourseId = courseId; command.ModuleId = moduleId; command.LessonId = lessonId; command.AdminId = AdminId;
        var created = await sender.Send(command, ct);
        return StatusCode(StatusCodes.Status201Created, created);
    }

    [HttpPost("{lessonId:guid}/resources/bulk")]
    public Task<AddLessonResourcesResult> AddResources(Guid courseId, Guid moduleId, Guid lessonId,
        AddLessonResourcesCommand command, CancellationToken ct)
    {
        command = command with { CourseId = courseId, ModuleId = moduleId, LessonId = lessonId, AdminId = AdminId };
        return sender.Send(command, ct);
    }

    [HttpDelete("{lessonId:guid}/resources/{resourceId:guid}")]
    public async Task<IActionResult> RemoveResource(Guid courseId, Guid moduleId, Guid lessonId, Guid resourceId, CancellationToken ct)
    {
        await sender.Send(new RemoveLessonResourceCommand(courseId, moduleId, lessonId, resourceId), ct);
        return NoContent();
    }

    [HttpGet("{lessonId:guid}/preview")]
    public Task<LessonPreviewDto> Preview(Guid courseId, Guid moduleId, Guid lessonId, CancellationToken ct) =>
        sender.Send(new GetLessonPreviewQuery(courseId, moduleId, lessonId), ct);
}
