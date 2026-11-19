using ToeicSpace.Payment.Application.Interfaces.Services;
using ToeicSpace.Payment.Domain.Entities;
using ToeicSpace.Payment.Domain.Enums;
using ToeicSpace.Payment.Domain.Exceptions;
using ToeicSpace.Payment.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ToeicSpace.Payment.Infrastructure.Services.Coupons;

public sealed class CouponService(PaymentDbContext db) : ICouponService
{
    public async Task<Coupon> CreateAsync(Coupon coupon, CancellationToken ct)
    {
        coupon.Validate();
        db.Coupons.Add(coupon);
        await db.SaveChangesAsync(ct);
        return coupon;
    }

    public Task<Coupon?> GetAsync(Guid id, CancellationToken ct) =>
        db.Coupons.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct);

    public async Task<IReadOnlyList<Coupon>> ListAsync(CancellationToken ct) =>
        await db.Coupons.AsNoTracking().OrderBy(x => x.Code).ToListAsync(ct);

    public async Task<Coupon> UpdateAsync(Guid id, Coupon values, CancellationToken ct)
    {
        values.Validate();
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var coupon = await db.Coupons.FromSqlInterpolated($"SELECT * FROM Coupons WHERE Id = {id} FOR UPDATE")
            .SingleOrDefaultAsync(ct) ?? throw new KeyNotFoundException("Coupon not found.");
        var active = db.CouponReservations.Where(x => x.CouponId == id &&
            (x.Status == CouponReservationStatus.Reserved || x.Status == CouponReservationStatus.Redeemed));
        if (values.UsageLimit is { } global && await active.CountAsync(ct) > global ||
            values.PerUserLimit is { } perUser && await active.GroupBy(x => x.UserId)
                .AnyAsync(g => g.Count() > perUser, ct))
            throw new InvalidOperationException("New limits are below existing usage.");
        coupon.Code = values.Code;
        coupon.Description = values.Description;
        coupon.DiscountType = values.DiscountType;
        coupon.DiscountValue = values.DiscountValue;
        coupon.MinimumOrderAmount = values.MinimumOrderAmount;
        coupon.MaximumDiscountAmount = values.MaximumDiscountAmount;
        coupon.StartAt = values.StartAt;
        coupon.ExpiresAt = values.ExpiresAt;
        coupon.UsageLimit = values.UsageLimit;
        coupon.PerUserLimit = values.PerUserLimit;
        coupon.IsActive = values.IsActive;
        coupon.ApplicableCourseIds = values.ApplicableCourseIds;
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return coupon;
    }

    public async Task<Coupon> DeactivateAsync(Guid id, CancellationToken ct)
    {
        var coupon = await db.Coupons.SingleOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new KeyNotFoundException("Coupon not found.");
        coupon.IsActive = false;
        await db.SaveChangesAsync(ct);
        return coupon;
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var coupon = await db.Coupons.FromSqlInterpolated($"SELECT * FROM Coupons WHERE Id = {id} FOR UPDATE")
            .SingleOrDefaultAsync(ct) ?? throw new KeyNotFoundException("Coupon not found.");
        var reservations = await db.CouponReservations.Where(x => x.CouponId == id).ToListAsync(ct);
        if (reservations.Any(x => x.Status != CouponReservationStatus.Released))
            throw new PaymentException("coupon_in_use", "Coupon has usage history and cannot be deleted.");
        db.CouponReservations.RemoveRange(reservations);
        db.Coupons.Remove(coupon);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
    }

    public async Task<decimal> PreviewAsync(string code, Guid courseId, decimal amount, Guid userId, CancellationToken ct)
    {
        var normalized = code.Trim().ToUpperInvariant();
        var coupon = await db.Coupons.AsNoTracking().SingleOrDefaultAsync(x => x.Code == normalized, ct)
            ?? throw new KeyNotFoundException("Coupon not found.");
        var taken = db.CouponReservations.Where(x => x.CouponId == coupon.Id &&
            (x.Status == CouponReservationStatus.Reserved || x.Status == CouponReservationStatus.Redeemed));
        if (coupon.UsageLimit is { } global && await taken.CountAsync(ct) >= global ||
            coupon.PerUserLimit is { } perUser && await taken.CountAsync(x => x.UserId == userId, ct) >= perUser)
            throw new InvalidOperationException("Coupon usage limit reached.");
        return coupon.CalculateDiscount(courseId, amount, DateTime.UtcNow);
    }
}
