using MediatR;
using ToeicSpace.Payment.Domain.Entities;
using ToeicSpace.Payment.Application.Interfaces.Services;

namespace ToeicSpace.Payment.Application.Features.Coupons.Commands.UpdateCoupon;

public sealed class UpdateCouponHandler(ICouponService coupons) : IRequestHandler<UpdateCouponCommand, Coupon>
{
    public Task<Coupon> Handle(UpdateCouponCommand request, CancellationToken ct) =>
        coupons.UpdateAsync(request.Id, request.Values, ct);
}
