using Microsoft.EntityFrameworkCore;
using ToeicSpace.Payment.Application.DTOs.Coupons;
using ToeicSpace.Payment.Application.Interfaces.Repositories;
using ToeicSpace.Payment.Domain.Entities;
using ToeicSpace.Payment.Domain.Enums;

namespace ToeicSpace.Payment.Infrastructure.Persistence.Repositories;

public sealed class CouponReadRepository(PaymentDbContext db) : ICouponReadRepository
{
    public async Task<CouponWithUsage?> GetWithUsageAsync(Guid id, CancellationToken ct)
    {
        var coupon = await db.Coupons.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct);
        if (coupon is null) return null;
        return (await WithUsageAsync([coupon], ct)).Single();
    }

    public async Task<CouponPage> SearchAsync(string? search, bool? isActive, int page, int pageSize, CancellationToken ct)
    {
        var query = db.Coupons.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(x => x.Code.Contains(term) || x.Description != null && x.Description.Contains(term));
        }
        if (isActive.HasValue) query = query.Where(x => x.IsActive == isActive.Value);
        var total = await query.CountAsync(ct);
        var items = await query.OrderBy(x => x.Code).ThenBy(x => x.Id)
            .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        return new CouponPage(page, pageSize, total, await WithUsageAsync(items, ct));
    }

    private async Task<IReadOnlyList<CouponWithUsage>> WithUsageAsync(IReadOnlyList<Coupon> coupons, CancellationToken ct)
    {
        if (coupons.Count == 0) return [];
        var ids = coupons.Select(x => x.Id).ToArray();
        var counts = await db.CouponReservations.AsNoTracking().Where(x => ids.Contains(x.CouponId) &&
                (x.Status == CouponReservationStatus.Redeemed || x.Status == CouponReservationStatus.Reserved))
            .GroupBy(x => new { x.CouponId, x.Status })
            .Select(x => new { x.Key.CouponId, x.Key.Status, Count = x.Count() }).ToListAsync(ct);
        var used = counts.Where(x => x.Status == CouponReservationStatus.Redeemed)
            .ToDictionary(x => x.CouponId, x => x.Count);
        var reserved = counts.Where(x => x.Status == CouponReservationStatus.Reserved)
            .ToDictionary(x => x.CouponId, x => x.Count);
        return coupons.Select(x => new CouponWithUsage(x, used.GetValueOrDefault(x.Id), reserved.GetValueOrDefault(x.Id),
            x.UsageLimit.HasValue ? Math.Max(0, x.UsageLimit.Value - used.GetValueOrDefault(x.Id) - reserved.GetValueOrDefault(x.Id)) : null)).ToArray();
    }
}
