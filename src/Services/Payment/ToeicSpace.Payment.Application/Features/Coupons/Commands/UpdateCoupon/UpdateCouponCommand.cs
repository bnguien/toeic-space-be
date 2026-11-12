using MediatR;
using ToeicSpace.Payment.Domain.Entities;

namespace ToeicSpace.Payment.Application.Features.Coupons.Commands.UpdateCoupon;

public sealed record UpdateCouponCommand(Guid Id, Coupon Values) : IRequest<Coupon>;
