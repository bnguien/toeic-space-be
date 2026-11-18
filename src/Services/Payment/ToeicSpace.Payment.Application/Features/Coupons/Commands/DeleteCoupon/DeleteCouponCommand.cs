using MediatR;

namespace ToeicSpace.Payment.Application.Features.Coupons.Commands.DeleteCoupon;

public sealed record DeleteCouponCommand(Guid Id) : IRequest;
