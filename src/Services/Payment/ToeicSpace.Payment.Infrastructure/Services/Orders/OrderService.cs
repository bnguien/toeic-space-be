using ToeicSpace.Payment.Application.DTOs.Orders;
using ToeicSpace.Payment.Domain.Exceptions;
using ToeicSpace.Payment.Application.Interfaces.Services;
using ToeicSpace.Payment.Domain.Entities;
using ToeicSpace.Payment.Domain.Enums;
using ToeicSpace.Payment.Infrastructure.Persistence;
using ToeicSpace.Payment.Infrastructure.Persistence.Repositories;
using ToeicSpace.Payment.Infrastructure.Services.Content;
using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace ToeicSpace.Payment.Infrastructure.Services.Orders;

public sealed class OrderService(PaymentDbContext db, ContentCheckoutClient content,
    ICouponService coupons, CouponReservationRepository reservations, IConfiguration config) : IOrderService
{
    private static string Normalize(string? code) => code?.Trim().ToUpperInvariant() ?? string.Empty;
    private int ReservationMinutes => config.GetValue("Checkout:ReservationMinutes", 15) is var minutes and > 0 and <= 60
        ? minutes : throw new InvalidOperationException("Invalid checkout reservation duration.");

    public async Task<PricePreview> PreviewAsync(Guid courseId, Guid userId, string? coupon, CancellationToken ct)
    {
        if (await db.Orders.AnyAsync(x => x.CourseId == courseId && x.UserId == userId && x.Status == OrderStatus.Paid, ct))
            throw new PaymentException("already_purchased", "A paid order for this course already exists.");
        var course = await content.CourseAsync(courseId, userId, ct);
        if (course.AlreadyEnrolled || course.Price <= 0 || course.Currency != "VND")
            throw new PaymentException("not_purchasable", "Course is free, already owned, or unavailable.");
        var discount = Normalize(coupon).Length == 0 ? 0 :
            await coupons.PreviewAsync(coupon!, courseId, course.Price, userId, ct);
        var promotion = await content.PromotionAsync(courseId, ct);
        if (promotion is not null && promotion.OriginalPrice == course.Price && promotion.DiscountAmount >= discount)
            return new PricePreview(courseId, course.Price, promotion.DiscountAmount, promotion.SellingPrice,
                "VND", OrderDiscountSource.Promotion, promotion.PromotionId, promotion.Name, promotion.ExpiresAt);
        return new PricePreview(courseId, course.Price, discount, course.Price - discount, "VND",
            discount > 0 ? OrderDiscountSource.Coupon : OrderDiscountSource.None);
    }

    public async Task<OrderResult> CheckoutAsync(Guid courseId, Guid userId, Guid reference, string? coupon, CancellationToken ct)
    {
        if (reference == Guid.Empty || userId == Guid.Empty)
            throw new PaymentException("invalid_checkout", "A valid checkout reference is required.", 400);
        var normalized = Normalize(coupon);
        var existing = await db.Orders.AsNoTracking().SingleOrDefaultAsync(x => x.CheckoutReference == reference, ct);
        if (existing is not null && (existing.CourseId != courseId || existing.UserId != userId ||
            (existing.RequestedCouponCode ?? existing.CouponCode) != (normalized.Length == 0 ? null : normalized)))
            throw new PaymentException("checkout_reference_conflict", "Checkout reference belongs to another request.");
        if (existing is not null && existing.Status != OrderStatus.Pending && existing.Status != OrderStatus.Cancelled)
            return Result(existing);
        if (await db.Orders.AnyAsync(x => x.CourseId == courseId && x.UserId == userId && x.Status == OrderStatus.Paid, ct))
            throw new PaymentException("already_purchased", "A paid order for this course already exists.");
        var pending = await db.Orders.AsNoTracking().Where(x => x.UserId == userId &&
            x.CourseId == courseId && x.Status == OrderStatus.Pending)
            .OrderByDescending(x => x.CreatedAt).FirstOrDefaultAsync(ct);
        if (pending is not null)
        {
            if ((pending.RequestedCouponCode ?? pending.CouponCode) != (normalized.Length == 0 ? null : normalized))
                throw new PaymentException("pending_checkout", "Cancel the existing pending order first.");
            return Result(pending); // A successfully created order is a price lock until it is settled.
        }
        else if (existing is { Status: OrderStatus.Cancelled })
            reference = Guid.NewGuid();
        else if (existing is not null)
            return Result(existing);
        var quote = await content.ReserveAsync(courseId, userId, reference, ct);
        if (quote.Price <= 0 || quote.Currency != "VND" || quote.AlreadyEnrolled)
        {
            if (quote.ReservationId is not null) await content.ReleaseAsync(reference, ct);
            throw new PaymentException("not_purchasable", "Only paid published courses can be purchased.");
        }
        var order = new PurchaseOrder
        {
            CheckoutReference = reference, UserId = userId, CourseId = courseId,
            OrderCode = "ORD" + Guid.NewGuid().ToString("N")[..12].ToUpperInvariant(),
            SlotReservationId = quote.ReservationId,
            PaymentReference = "TS" + Guid.NewGuid().ToString("N")[..12].ToUpperInvariant(),
            OriginalAmount = quote.Price, FinalAmount = quote.Price, CreatedAt = DateTime.UtcNow,
            ExpiresAt = quote.ExpiresAt ?? DateTime.UtcNow.AddMinutes(ReservationMinutes),
            RequestedCouponCode = normalized.Length == 0 ? null : normalized,
            PendingKey = $"{userId:N}:{courseId:N}"
        };
        var reservedPromotion = false;
        try
        {
            var promotion = await content.PromotionAsync(courseId, ct);
            var couponDiscount = normalized.Length == 0 ? 0m :
                await coupons.PreviewAsync(normalized, courseId, quote.Price, userId, ct);
            var usePromotion = promotion is not null && promotion.OriginalPrice == quote.Price &&
                promotion.DiscountAmount >= couponDiscount;
            if (usePromotion)
            {
                try
                {
                    var reserved = await content.ReservePromotionAsync(courseId, promotion!.PromotionId, reference,
                        quote.Price, order.ExpiresAt, ct);
                    reservedPromotion = true;
                    if (reserved.OriginalPrice != quote.Price || reserved.DiscountAmount != promotion.DiscountAmount ||
                        reserved.SellingPrice != quote.Price - reserved.DiscountAmount)
                        throw new PaymentException("promotion_changed", "Promotion changed during checkout; retry.", 409);
                    order.DiscountSource = OrderDiscountSource.Promotion;
                    order.PromotionId = promotion.PromotionId;
                    order.PromotionName = promotion.Name;
                    order.PromotionExpiresAt = DateTime.SpecifyKind(promotion.ExpiresAt, DateTimeKind.Utc);
                    order.DiscountAmount = reserved.DiscountAmount;
                    order.PromotionConfirmPending = reserved.DiscountAmount == quote.Price;
                }
                catch (PaymentException ex) when (ex.Code == "course_unavailable" && !reservedPromotion)
                {
                    // The promotion reached its limit or expired between preview and reservation.
                    // Coupon eligibility was already validated; use it (or the original price).
                    usePromotion = false;
                }
            }
            var created = await reservations.CreateOrderAsync(order, usePromotion || normalized.Length == 0 ? null : normalized, ct);
            return Result(created);
        }
        catch
        {
            // If the DB committed despite a lost response, do not release a potentially paid entitlement.
            var stored = await db.Orders.AsNoTracking().SingleOrDefaultAsync(x => x.CheckoutReference == reference, CancellationToken.None);
            if (stored is not null) return Resume(stored, courseId, userId, normalized);
            if (reservedPromotion)
                try { await content.SettlePromotionAsync(reference, false, CancellationToken.None); }
                catch { /* Content reconciles orphan promotion reservations with Payment. */ }
            if (quote.ReservationId is not null)
            {
                try { await content.ReleaseAsync(reference, CancellationToken.None); }
                catch { /* Orphan reservations require the Content reconciliation worker. */ }
            }
            throw;
        }
    }

    public async Task<OrderResult> StatusAsync(Guid id, Guid userId, CancellationToken ct)
    {
        var order = await db.Orders.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id && x.UserId == userId, ct)
            ?? throw new PaymentException("not_found", "Order not found.", 404);
        return Result(order);
    }

    public async Task CancelAsync(Guid id, Guid userId, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var order = await db.Orders.FromSqlInterpolated($"SELECT * FROM PurchaseOrders WHERE Id = {id} FOR UPDATE")
            .SingleOrDefaultAsync(ct);
        if (order is null || order.UserId != userId) throw new PaymentException("not_found", "Order not found.", 404);
        if (order.Status != OrderStatus.Pending) throw new PaymentException("invalid_state", "Order cannot be cancelled.");
        await reservations.SettleWithinTransactionAsync(order.Id, false, ct);
        order.Status = OrderStatus.Cancelled;
        order.PendingKey = null;
        order.ReleasePending = true;
        if (order.DiscountSource == OrderDiscountSource.Promotion) order.PromotionReleasePending = true;
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        // Best-effort immediate release; durable flags let the worker retry after failure.
        try
        {
            await content.ReleaseAsync(order.CheckoutReference, ct);
            await db.Orders.Where(x => x.Id == order.Id && x.Status == OrderStatus.Cancelled)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.ReleasePending, false), ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException) { /* Worker retries. */ }
        if (order.DiscountSource == OrderDiscountSource.Promotion)
            try
            {
                await content.SettlePromotionAsync(order.CheckoutReference, false, ct);
                await db.Orders.Where(x => x.Id == order.Id && x.Status == OrderStatus.Cancelled)
                    .ExecuteUpdateAsync(s => s.SetProperty(x => x.PromotionReleasePending, false), ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException) { /* Worker retries. */ }
    }

    private OrderResult Resume(PurchaseOrder order, Guid courseId, Guid userId, string code)
    {
        if (order.CourseId != courseId || order.UserId != userId ||
            (order.RequestedCouponCode ?? order.CouponCode) != (code.Length == 0 ? null : code))
            throw new PaymentException("checkout_reference_conflict", "Checkout reference belongs to another request.");
        return Result(order);
    }

    private OrderResult Result(PurchaseOrder order)
    {
        var bank = config["SePay:Bank"];
        var account = config["SePay:AccountNumber"];
        if (order.Status != OrderStatus.Pending || order.FinalAmount == 0)
            return new OrderResult(order.Id, order.OrderCode, order.CourseId, order.Status, order.Fulfillment,
                order.OriginalAmount, order.CouponCode, order.DiscountAmount, order.FinalAmount,
                order.Currency, DateTime.SpecifyKind(order.ExpiresAt, DateTimeKind.Utc), null, null, null, null,
                order.DiscountSource, order.PromotionId, order.PromotionName,
                order.PromotionExpiresAt is { } promotionDeadline ? DateTime.SpecifyKind(promotionDeadline, DateTimeKind.Utc) : null);
        if (string.IsNullOrWhiteSpace(bank) || string.IsNullOrWhiteSpace(account))
            throw new PaymentException("payment_not_configured", "Bank account is not configured.", 503);
        var qr = "https://vietqr.app/img?acc=" + Uri.EscapeDataString(account) +
            "&bank=" + Uri.EscapeDataString(bank) +
            "&amount=" + order.FinalAmount.ToString("0", CultureInfo.InvariantCulture) +
            "&des=" + Uri.EscapeDataString(order.PaymentReference) + "&template=compact";
        return new OrderResult(order.Id, order.OrderCode, order.CourseId, order.Status, order.Fulfillment,
            order.OriginalAmount, order.CouponCode, order.DiscountAmount, order.FinalAmount,
            order.Currency, DateTime.SpecifyKind(order.ExpiresAt, DateTimeKind.Utc), order.PaymentReference, bank, account, qr,
            order.DiscountSource, order.PromotionId, order.PromotionName,
            order.PromotionExpiresAt is { } expiry ? DateTime.SpecifyKind(expiry, DateTimeKind.Utc) : null);
    }
}
