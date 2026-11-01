using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ToeicSpace.Content.Application.DTOs.Modules;
using ToeicSpace.Content.Application.Features.Modules.Commands.CreateModule;
using ToeicSpace.Content.Application.Features.Modules.Commands.DeleteModule;
using ToeicSpace.Content.Application.Features.Modules.Commands.ReorderModules;
using ToeicSpace.Content.Application.Features.Modules.Commands.UpdateModule;
using ToeicSpace.Content.Application.Features.Modules.Queries.GetModuleById;
using ToeicSpace.Content.Application.Features.Modules.Queries.GetModulesByCourse;

namespace ToeicSpace.Content.API.Controllers;

[ApiController]
[Route("api/v1/courses/{courseId:guid}/modules")]
[Authorize(Policy = "Admin")]
public sealed class CourseModulesController(ISender sender) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<ModuleDto>> Create(Guid courseId, CreateModuleCommand command, CancellationToken ct)
    {
        command.CourseId = courseId;
        var created = await sender.Send(command, ct);
        return CreatedAtAction(nameof(Get), new { courseId, moduleId = created.Id }, created);
    }

    [HttpGet]
    public Task<IReadOnlyList<ModuleDto>> List(Guid courseId, CancellationToken ct) =>
        sender.Send(new GetModulesByCourseQuery(courseId), ct);

    [HttpGet("{moduleId:guid}")]
    public Task<ModuleDto> Get(Guid courseId, Guid moduleId, CancellationToken ct) =>
        sender.Send(new GetModuleByIdQuery(courseId, moduleId), ct);

    [HttpPut("{moduleId:guid}")]
    public Task<ModuleDto> Update(Guid courseId, Guid moduleId, UpdateModuleCommand command, CancellationToken ct)
    {
        command.CourseId = courseId;
        command.ModuleId = moduleId;
        return sender.Send(command, ct);
    }

    [HttpDelete("{moduleId:guid}")]
    public async Task<IActionResult> Delete(Guid courseId, Guid moduleId, CancellationToken ct)
    {
        await sender.Send(new DeleteModuleCommand(courseId, moduleId), ct);
        return NoContent();
    }

    [HttpPut("order")]
    public Task<IReadOnlyList<ModuleDto>> Reorder(Guid courseId, ReorderModulesCommand command, CancellationToken ct)
    {
        command.CourseId = courseId;
        return sender.Send(command, ct);
    }
}
