using MediatR;
using ToeicSpace.Payment.Domain.Entities;
using ToeicSpace.Payment.Application.Interfaces.Services;

namespace ToeicSpace.Payment.Application.Features.Coupons.Commands.CreateCoupon;

public sealed class CreateCouponHandler(ICouponService coupons) : IRequestHandler<CreateCouponCommand, Coupon>
{
    public Task<Coupon> Handle(CreateCouponCommand request, CancellationToken ct)
    {
        request.Values.Id = Guid.NewGuid();
        return coupons.CreateAsync(request.Values, ct);
    }
}
