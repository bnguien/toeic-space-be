using MediatR;
using ToeicSpace.Payment.Application.DTOs.Orders;
using ToeicSpace.Payment.Application.Interfaces.Services;

namespace ToeicSpace.Payment.Application.Features.Orders.Queries.GetOrder;

public sealed class GetOrderHandler(IOrderService orders) : IRequestHandler<GetOrderQuery, OrderResult>
{
    public Task<OrderResult> Handle(GetOrderQuery request, CancellationToken ct) =>
        orders.StatusAsync(request.Id, request.UserId, ct);
}
