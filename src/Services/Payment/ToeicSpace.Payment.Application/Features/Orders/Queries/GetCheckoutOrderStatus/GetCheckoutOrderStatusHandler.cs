using MediatR;
using ToeicSpace.Payment.Application.DTOs.Orders;
using ToeicSpace.Payment.Application.Interfaces.Repositories;

namespace ToeicSpace.Payment.Application.Features.Orders.Queries.GetCheckoutOrderStatus;

public sealed class GetCheckoutOrderStatusHandler(IPaymentReadRepository orders)
    : IRequestHandler<GetCheckoutOrderStatusQuery, CheckoutOrderState?>
{
    public Task<CheckoutOrderState?> Handle(GetCheckoutOrderStatusQuery request, CancellationToken ct) =>
        orders.GetCheckoutStatusAsync(request.CheckoutReference, ct);
}
