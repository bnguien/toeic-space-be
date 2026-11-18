using MediatR;
using ToeicSpace.Payment.Application.DTOs.Orders;
using ToeicSpace.Payment.Application.Interfaces.Services;

namespace ToeicSpace.Payment.Application.Features.Orders.Commands.CreateOrder;

public sealed class CreateOrderHandler(IOrderService orders) : IRequestHandler<CreateOrderCommand, OrderResult>
{
    public Task<OrderResult> Handle(CreateOrderCommand request, CancellationToken ct) =>
        orders.CheckoutAsync(request.CourseId, request.UserId, request.CheckoutReference, request.CouponCode, ct);
}
