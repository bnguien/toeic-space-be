using MediatR;
using ToeicSpace.Payment.Application.Interfaces.Services;

namespace ToeicSpace.Payment.Application.Features.Coupons.Commands.DeleteCoupon;

public sealed class DeleteCouponHandler(ICouponService coupons) : IRequestHandler<DeleteCouponCommand>
{
    public Task Handle(DeleteCouponCommand request, CancellationToken ct) =>
        coupons.DeleteAsync(request.Id, ct);
}
