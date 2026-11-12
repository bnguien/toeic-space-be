using MediatR;
using ToeicSpace.Payment.Application.DTOs.Coupons;

namespace ToeicSpace.Payment.Application.Features.Coupons.Queries.ListCoupons;

public sealed record ListCouponsQuery(string? Search = null, bool? IsActive = null, int Page = 1, int PageSize = 20)
    : IRequest<CouponPage>;
