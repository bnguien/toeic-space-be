using ToeicSpace.Payment.Application.DTOs.Orders;
using ToeicSpace.Payment.Domain.Exceptions;
using MediatR;
using ToeicSpace.Payment.Application.Features.Orders.Commands.CreateOrder;
using ToeicSpace.Payment.Application.Features.Orders.Commands.CancelOrder;
using ToeicSpace.Payment.Application.Features.Orders.Queries.PreviewOrderPrice;
using ToeicSpace.Payment.Application.Features.Orders.Queries.GetOrder;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ToeicSpace.BuildingBlocks.Security.Jwt;

namespace ToeicSpace.Payment.API.Controllers;

[ApiController]
[Route("api/v1/orders")]
[Authorize(Roles = "User")]
public sealed class OrdersController(ISender sender) : ControllerBase
{
    private Guid UserId => Guid.TryParse(User.FindFirst(AccessTokenDefaults.SubjectClaim)?.Value, out var id)
        ? id : throw new PaymentException("invalid_subject", "Invalid user identity.", 401);

    [HttpGet("preview")]
    public Task<PricePreview> Preview([FromQuery] Guid courseId, [FromQuery] string? couponCode, CancellationToken ct) =>
        sender.Send(new PreviewOrderPriceQuery(courseId, UserId, couponCode), ct);

    [HttpPost]
    public Task<OrderResult> Checkout([FromBody] CheckoutRequest request, CancellationToken ct) =>
        sender.Send(new CreateOrderCommand(request.CourseId, UserId, request.CheckoutReference, request.CouponCode), ct);

    [HttpGet("{id:guid}")]
    public Task<OrderResult> Status(Guid id, CancellationToken ct) => sender.Send(new GetOrderQuery(id, UserId), ct);

    [HttpPost("{id:guid}/cancel")]
    public Task Cancel(Guid id, CancellationToken ct) => sender.Send(new CancelOrderCommand(id, UserId), ct);

    public sealed record CheckoutRequest(Guid CourseId, Guid CheckoutReference, string? CouponCode);
}
