using MediatR;
using ToeicSpace.Payment.Domain.Entities;
using ToeicSpace.Payment.Application.Interfaces.Services;

namespace ToeicSpace.Payment.Application.Features.Coupons.Commands.DeactivateCoupon;

public sealed class DeactivateCouponHandler(ICouponService coupons) : IRequestHandler<DeactivateCouponCommand, Coupon>
{
    public Task<Coupon> Handle(DeactivateCouponCommand request, CancellationToken ct) =>
        coupons.DeactivateAsync(request.Id, ct);
}
