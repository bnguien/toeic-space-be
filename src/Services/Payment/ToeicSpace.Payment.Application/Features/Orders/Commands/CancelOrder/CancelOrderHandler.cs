using MediatR;
using ToeicSpace.Payment.Application.Interfaces.Services;

namespace ToeicSpace.Payment.Application.Features.Orders.Commands.CancelOrder;

public sealed class CancelOrderHandler(IOrderService orders) : IRequestHandler<CancelOrderCommand>
{
    public Task Handle(CancelOrderCommand request, CancellationToken ct) =>
        orders.CancelAsync(request.Id, request.UserId, ct);
}
