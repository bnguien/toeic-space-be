using MediatR;
using ToeicSpace.Payment.Domain.Entities;

namespace ToeicSpace.Payment.Application.Features.Coupons.Commands.CreateCoupon;

public sealed record CreateCouponCommand(Coupon Values) : IRequest<Coupon>;
