using MediatR;
using ToeicSpace.Payment.Application.DTOs.Coupons;
using ToeicSpace.Payment.Application.Interfaces.Repositories;
using ToeicSpace.Payment.Domain.Exceptions;

namespace ToeicSpace.Payment.Application.Features.Coupons.Queries.ListCoupons;

public sealed class ListCouponsHandler(ICouponReadRepository coupons) : IRequestHandler<ListCouponsQuery, CouponPage>
{
    public Task<CouponPage> Handle(ListCouponsQuery request, CancellationToken ct)
    {
        if (request.Page < 1 || request.PageSize is < 1 or > 100 || request.Page > int.MaxValue / request.PageSize)
            throw new PaymentException("invalid_pagination", "Invalid page or page size.", 400);
        return coupons.SearchAsync(request.Search, request.IsActive, request.Page, request.PageSize, ct);
    }
}
