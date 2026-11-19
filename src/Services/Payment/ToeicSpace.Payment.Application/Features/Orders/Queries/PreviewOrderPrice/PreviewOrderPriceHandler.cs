using MediatR;
using ToeicSpace.Payment.Application.DTOs.Orders;
using ToeicSpace.Payment.Application.Interfaces.Services;

namespace ToeicSpace.Payment.Application.Features.Orders.Queries.PreviewOrderPrice;

public sealed class PreviewOrderPriceHandler(IOrderService orders) : IRequestHandler<PreviewOrderPriceQuery, PricePreview>
{
    public Task<PricePreview> Handle(PreviewOrderPriceQuery request, CancellationToken ct) =>
        orders.PreviewAsync(request.CourseId, request.UserId, request.CouponCode, ct);
}
