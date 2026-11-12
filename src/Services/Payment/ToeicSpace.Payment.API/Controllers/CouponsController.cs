using MediatR;
using ToeicSpace.Payment.Application.Features.Coupons.Commands.CreateCoupon;
using ToeicSpace.Payment.Application.Features.Coupons.Commands.UpdateCoupon;
using ToeicSpace.Payment.Application.Features.Coupons.Commands.DeactivateCoupon;
using ToeicSpace.Payment.Application.Features.Coupons.Commands.DeleteCoupon;
using ToeicSpace.Payment.Application.Features.Coupons.Queries.GetCoupon;
using ToeicSpace.Payment.Application.Features.Coupons.Queries.ListCoupons;
using ToeicSpace.Payment.Domain.Entities;
using ToeicSpace.Payment.Application.DTOs.Coupons;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ToeicSpace.Payment.API.Controllers;

[ApiController]
[Route("api/v1/admin/coupons")]
[Authorize(Roles = "Admin")]
public sealed class CouponsController(ISender sender) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<Coupon>> Create([FromBody] Coupon coupon, CancellationToken ct)
    {
        var created = await sender.Send(new CreateCouponCommand(coupon), ct);
        return CreatedAtAction(nameof(Get), new { id = created.Id }, created);
    }

    [HttpGet]
    public Task<CouponPage> List([FromQuery] string? search, [FromQuery] bool? isActive,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default) =>
        sender.Send(new ListCouponsQuery(search, isActive, page, pageSize), ct);

    [HttpGet("{id:guid}")]
    public Task<CouponWithUsage> Get(Guid id, CancellationToken ct) => sender.Send(new GetCouponQuery(id), ct);

    [HttpPut("{id:guid}")]
    public Task<Coupon> Update(Guid id, [FromBody] Coupon coupon, CancellationToken ct) =>
        sender.Send(new UpdateCouponCommand(id, coupon), ct);

    [HttpPost("{id:guid}/deactivate")]
    public Task<Coupon> Deactivate(Guid id, CancellationToken ct) => sender.Send(new DeactivateCouponCommand(id), ct);

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await sender.Send(new DeleteCouponCommand(id), ct);
        return NoContent();
    }
}
