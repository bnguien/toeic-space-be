using ToeicSpace.Payment.Domain.Exceptions;
using ToeicSpace.Payment.Domain.Entities;
using ToeicSpace.Payment.Domain.Enums;
using ToeicSpace.Payment.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using MySqlConnector;

namespace ToeicSpace.Payment.Infrastructure.Persistence.Repositories;

public sealed class CouponReservationRepository(PaymentDbContext db)
{
    public async Task<PurchaseOrder> CreateOrderAsync(PurchaseOrder order, string? code, CancellationToken ct)
    {
        for (var attempt = 0; ; attempt++)
        {
            try { return await CreateOnceAsync(order, code, ct); }
            catch (MySqlException ex) when (ex.Number is 1205 or 1213 && attempt < 2)
            {
                db.ChangeTracker.Clear();
                await Task.Delay(TimeSpan.FromMilliseconds(50 * (attempt + 1)), ct);
            }
            catch (DbUpdateException ex) when (ex.InnerException is MySqlException { Number: 1205 or 1213 } && attempt < 2)
            {
                db.ChangeTracker.Clear();
                await Task.Delay(TimeSpan.FromMilliseconds(50 * (attempt + 1)), ct);
            }
        }
    }

    private async Task<PurchaseOrder> CreateOnceAsync(PurchaseOrder order, string? code, CancellationToken ct)
    {
        if (DateTime.UtcNow >= order.ExpiresAt)
            throw new PaymentException("checkout_expired", "Checkout reservation has expired.");
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        if (!string.IsNullOrWhiteSpace(code))
        {
            var normalized = code.Trim().ToUpperInvariant();
            var coupon = await db.Coupons.FromSqlInterpolated($"SELECT * FROM Coupons WHERE Code = {normalized} FOR UPDATE")
                .SingleOrDefaultAsync(ct) ?? throw new PaymentException("invalid_coupon", "Coupon not found.");
            var discount = coupon.CalculateDiscount(order.CourseId, order.OriginalAmount, DateTime.UtcNow);
            if (order.ExpiresAt > coupon.ExpiresAt) order.ExpiresAt = coupon.ExpiresAt;
            var taken = db.CouponReservations.Where(x => x.CouponId == coupon.Id &&
                (x.Status == CouponReservationStatus.Reserved || x.Status == CouponReservationStatus.Redeemed));
            if (coupon.UsageLimit is { } global && await taken.CountAsync(ct) >= global ||
                coupon.PerUserLimit is { } perUser && await taken.CountAsync(x => x.UserId == order.UserId, ct) >= perUser)
                throw new PaymentException("coupon_limit", "Coupon usage limit reached.");
            order.CouponCode = coupon.Code;
            order.DiscountSource = OrderDiscountSource.Coupon;
            order.DiscountAmount = discount;
            order.FinalAmount = order.OriginalAmount - discount;
            db.CouponReservations.Add(new CouponReservation
            {
                CouponId = coupon.Id, OrderId = order.Id, UserId = order.UserId,
                Status = order.FinalAmount == 0 ? CouponReservationStatus.Redeemed : CouponReservationStatus.Reserved,
                ExpiresAt = order.ExpiresAt, CreatedAt = DateTime.UtcNow,
                RedeemedAt = order.FinalAmount == 0 ? DateTime.UtcNow : null
            });
        }
        else if (order.DiscountSource == OrderDiscountSource.Promotion)
            order.FinalAmount = order.OriginalAmount - order.DiscountAmount;
        if (order.FinalAmount == 0)
        {
            order.Status = OrderStatus.Paid;
            order.PaidAt = DateTime.UtcNow;
            order.Fulfillment = FulfillmentStatus.Pending;
            order.NextAttemptAt = DateTime.UtcNow;
        }
        db.Orders.Add(order);
        if (DateTime.UtcNow >= order.ExpiresAt)
            throw new PaymentException("checkout_expired", "Checkout reservation has expired.");
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return order;
    }

    // Call only inside an order transaction after locking its Order row.
    public async Task SettleWithinTransactionAsync(Guid orderId, bool paid, CancellationToken ct)
    {
        var id = await db.CouponReservations.AsNoTracking().Where(x => x.OrderId == orderId)
            .Select(x => x.CouponId).SingleOrDefaultAsync(ct);
        if (id == Guid.Empty) return;
        await db.Coupons.FromSqlInterpolated($"SELECT * FROM Coupons WHERE Id = {id} FOR UPDATE")
            .SingleAsync(ct);
        var reservation = await db.CouponReservations.SingleAsync(x => x.OrderId == orderId, ct);
        if (reservation.Status == (paid ? CouponReservationStatus.Redeemed : CouponReservationStatus.Released)) return;
        if (reservation.Status != CouponReservationStatus.Reserved)
            throw new PaymentException("coupon_state", "Coupon reservation has already been settled.");
        reservation.Status = paid ? CouponReservationStatus.Redeemed : CouponReservationStatus.Released;
        if (paid) reservation.RedeemedAt = DateTime.UtcNow;
    }
}
