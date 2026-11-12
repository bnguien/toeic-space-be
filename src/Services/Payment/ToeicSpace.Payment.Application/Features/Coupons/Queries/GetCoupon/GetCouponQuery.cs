using MediatR;
using ToeicSpace.Payment.Application.DTOs.Coupons;

namespace ToeicSpace.Payment.Application.Features.Coupons.Queries.GetCoupon;

public sealed record GetCouponQuery(Guid Id) : IRequest<CouponWithUsage>;
