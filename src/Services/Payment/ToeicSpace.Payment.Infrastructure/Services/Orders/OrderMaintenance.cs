using ToeicSpace.Payment.Domain.Exceptions;
using ToeicSpace.Payment.Domain.Enums;
using ToeicSpace.Payment.Infrastructure.Persistence;
using ToeicSpace.Payment.Infrastructure.Persistence.Repositories;
using ToeicSpace.Payment.Infrastructure.Services.Content;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ToeicSpace.Payment.Infrastructure.Services.Orders;

public sealed class OrderMaintenance(IServiceScopeFactory scopes, ILogger<OrderMaintenance> log) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(15));
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                await scope.ServiceProvider.GetRequiredService<OrderMaintenanceRunner>().RunAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception ex) { log.LogError(ex, "Payment maintenance failed; will retry."); }
            if (!await timer.WaitForNextTickAsync(stoppingToken)) break;
        }
    }
}

public sealed class OrderMaintenanceRunner(PaymentDbContext db, CouponReservationRepository coupons,
    ContentCheckoutClient content, ILogger<OrderMaintenanceRunner> log)
{
    public async Task RunAsync(CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        // Compare two independent UTC clocks before any irreversible expiry/release.
        // A host whose clock is hours ahead must not expire live checkouts.
        var databaseNow = await db.Database.SqlQuery<DateTime>($"SELECT UTC_TIMESTAMP(6) AS Value").SingleAsync(ct);
        if (Math.Abs((now - databaseNow).TotalSeconds) > 60)
            log.LogError("Payment expiry skipped: host UTC clock {HostUtc:o} differs from MySQL UTC clock {DatabaseUtc:o}.", now, databaseNow);
        else
        {
            var due = await db.Orders.AsNoTracking().Where(x => x.Status == OrderStatus.Pending && x.ExpiresAt < databaseNow)
                .OrderBy(x => x.ExpiresAt).Select(x => x.Id).Take(50).ToListAsync(ct);
            foreach (var id in due)
            {
                try { await ExpireAsync(id, ct); }
                catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
                catch (Exception ex)
                {
                    log.LogError(ex, "Expiry of order {OrderId} failed; will retry.", id);
                    db.ChangeTracker.Clear();
                }
            }
        }
        var releases = await db.Orders.AsNoTracking().Where(x => x.ReleasePending &&
            (x.NextAttemptAt == null || x.NextAttemptAt <= now)).Select(x => x.Id).Take(50).ToListAsync(ct);
        foreach (var id in releases) await ReleaseAsync(id, ct);
        var promotionReleases = await db.Orders.AsNoTracking().Where(x => x.PromotionReleasePending)
            .Select(x => x.Id).Take(50).ToListAsync(ct);
        foreach (var id in promotionReleases) await SettlePromotionAsync(id, false, ct);
        var promotionConfirms = await db.Orders.AsNoTracking().Where(x => x.PromotionConfirmPending && x.Status == OrderStatus.Paid)
            .Select(x => x.Id).Take(50).ToListAsync(ct);
        foreach (var id in promotionConfirms) await SettlePromotionAsync(id, true, ct);
        var fulfills = await db.Orders.AsNoTracking().Where(x => x.Status == OrderStatus.Paid &&
            x.Fulfillment == FulfillmentStatus.Pending && x.NextAttemptAt <= now)
            .OrderBy(x => x.NextAttemptAt).Select(x => x.Id).Take(50).ToListAsync(ct);
        foreach (var id in fulfills) await FulfillAsync(id, ct);
    }

    private async Task ExpireAsync(Guid id, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var order = await db.Orders.FromSqlInterpolated($"SELECT * FROM PurchaseOrders WHERE Id = {id} FOR UPDATE")
            .SingleAsync(ct);
        var databaseNow = await db.Database.SqlQuery<DateTime>($"SELECT UTC_TIMESTAMP(6) AS Value").SingleAsync(ct);
        if (order.Status != OrderStatus.Pending || order.ExpiresAt >= databaseNow ||
            Math.Abs((DateTime.UtcNow - databaseNow).TotalSeconds) > 60) return;
        log.LogInformation("Expiring payment order {OrderId}: expiresAt UTC {ExpiresAt:o}, database UTC {DatabaseUtc:o}.",
            order.Id, order.ExpiresAt, databaseNow);
        await coupons.SettleWithinTransactionAsync(id, false, ct);
        order.Status = OrderStatus.Expired;
        order.PendingKey = null;
        order.ReleasePending = true;
        if (order.DiscountSource == OrderDiscountSource.Promotion) order.PromotionReleasePending = true;
        order.NextAttemptAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
    }

    private async Task ReleaseAsync(Guid id, CancellationToken ct)
    {
        var order = await db.Orders.AsNoTracking().SingleAsync(x => x.Id == id, ct);
        if (order.Status is not (OrderStatus.Expired or OrderStatus.Cancelled)) return;
        try
        {
            await content.ReleaseAsync(order.CheckoutReference, ct);
            await db.Orders.Where(x => x.Id == id && x.ReleasePending && x.Status != OrderStatus.Paid)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.ReleasePending, false), ct);
        }
        catch (Exception ex)
        {
            log.LogWarning("Slot release for order {OrderId} will retry: {Type}", id, ex.GetType().Name);
            await BackoffAsync(id, ct);
        }
    }

    private async Task FulfillAsync(Guid id, CancellationToken ct)
    {
        var order = await db.Orders.AsNoTracking().SingleAsync(x => x.Id == id, ct);
        if (order.Status != OrderStatus.Paid || order.PaidAt is null) return;
        if (order.PromotionConfirmPending) return;
        try
        {
            await content.FulfillAsync(order.CourseId, order.UserId, order.CheckoutReference, order.PaidAt.Value,
                order.SlotReservationId is not null, ct);
            await db.Orders.Where(x => x.Id == id && x.Status == OrderStatus.Paid &&
                x.Fulfillment == FulfillmentStatus.Pending)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.Fulfillment, FulfillmentStatus.Completed)
                    .SetProperty(x => x.NextAttemptAt, (DateTime?)null), ct);
        }
        catch (PaymentException ex) when (ex.Code is "course_unavailable" or "course_not_found")
        {
            log.LogWarning("Paid order {OrderId} requires manual entitlement reconciliation.", id);
            await db.Orders.Where(x => x.Id == id && x.Fulfillment == FulfillmentStatus.Pending)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.Fulfillment, FulfillmentStatus.NeedsReview), ct);
        }
        catch (Exception ex)
        {
            log.LogWarning("Fulfillment for order {OrderId} will retry: {Type}", id, ex.GetType().Name);
            await BackoffAsync(id, ct);
        }
    }

    private async Task SettlePromotionAsync(Guid id, bool paid, CancellationToken ct)
    {
        var order = await db.Orders.AsNoTracking().SingleAsync(x => x.Id == id, ct);
        if (order.DiscountSource != OrderDiscountSource.Promotion ||
            (paid ? order.Status != OrderStatus.Paid : order.Status is not (OrderStatus.Cancelled or OrderStatus.Expired))) return;
        try
        {
            await content.SettlePromotionAsync(order.CheckoutReference, paid, ct);
            if (paid)
                await db.Orders.Where(x => x.Id == id && x.Status == OrderStatus.Paid)
                    .ExecuteUpdateAsync(s => s.SetProperty(x => x.PromotionConfirmPending, false), ct);
            else
                await db.Orders.Where(x => x.Id == id && x.Status != OrderStatus.Paid)
                    .ExecuteUpdateAsync(s => s.SetProperty(x => x.PromotionReleasePending, false), ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        { log.LogWarning("Promotion settlement for order {OrderId} will retry: {Type}", id, ex.GetType().Name); }
    }

    private async Task BackoffAsync(Guid id, CancellationToken ct)
    {
        var order = await db.Orders.SingleAsync(x => x.Id == id, ct);
        order.AttemptCount++;
        order.NextAttemptAt = DateTime.UtcNow.AddSeconds(Math.Min(300, 5 * Math.Pow(2, Math.Min(6, order.AttemptCount))));
        await db.SaveChangesAsync(ct);
        db.Entry(order).State = EntityState.Detached;
    }
}
