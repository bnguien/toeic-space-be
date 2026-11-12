using MediatR;
using ToeicSpace.Payment.Application.DTOs.Coupons;
using ToeicSpace.Payment.Application.Interfaces.Repositories;

namespace ToeicSpace.Payment.Application.Features.Coupons.Queries.GetCoupon;

public sealed class GetCouponHandler(ICouponReadRepository coupons) : IRequestHandler<GetCouponQuery, CouponWithUsage>
{
    public async Task<CouponWithUsage> Handle(GetCouponQuery request, CancellationToken ct) =>
        await coupons.GetWithUsageAsync(request.Id, ct) ?? throw new KeyNotFoundException("Coupon not found.");
}
