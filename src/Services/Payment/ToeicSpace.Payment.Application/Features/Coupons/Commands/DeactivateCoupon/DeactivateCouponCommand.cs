using MediatR;
using ToeicSpace.Payment.Domain.Entities;

namespace ToeicSpace.Payment.Application.Features.Coupons.Commands.DeactivateCoupon;

public sealed record DeactivateCouponCommand(Guid Id) : IRequest<Coupon>;
