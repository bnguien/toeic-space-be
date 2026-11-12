using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ToeicSpace.Content.Application.Features.Courses.Promotions;
using ToeicSpace.Content.Application.Interfaces.Services;
using ToeicSpace.Content.Domain.Entities;

namespace ToeicSpace.Content.API.Controllers;

[ApiController]
[Route("api/v1/admin/course-promotions")]
[Authorize(Roles = "Admin")]
public sealed class CoursePromotionsController(ISender sender) : ControllerBase
{
    [HttpPost("preview/{courseId:guid}")]
    public Task<PromotionPrice> Preview(Guid courseId, CoursePromotion value, CancellationToken ct) =>
        sender.Send(new PreviewPromotionQuery(courseId, value), ct);
    [HttpGet]
    public Task<IReadOnlyList<PromotionView>> List(CancellationToken ct) => sender.Send(new ListPromotionsQuery(), ct);

    [HttpPost]
    public async Task<ActionResult<PromotionView>> Create(CoursePromotion promotion, CancellationToken ct)
    {
        var result = await sender.Send(new SavePromotionCommand(null, promotion), ct);
        return Created($"/api/v1/admin/course-promotions/{result.Promotion.Id}", result);
    }

    [HttpPut("{id:guid}")]
    public Task<PromotionView> Update(Guid id, CoursePromotion promotion, CancellationToken ct) =>
        sender.Send(new SavePromotionCommand(id, promotion), ct);

    [HttpPost("{id:guid}/activate")]
    public Task<PromotionView> Activate(Guid id, CancellationToken ct) =>
        sender.Send(new SetPromotionActiveCommand(id, true), ct);

    [HttpPost("{id:guid}/deactivate")]
    public Task<PromotionView> Deactivate(Guid id, CancellationToken ct) =>
        sender.Send(new SetPromotionActiveCommand(id, false), ct);

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await sender.Send(new DeletePromotionCommand(id), ct);
        return NoContent();
    }
}
