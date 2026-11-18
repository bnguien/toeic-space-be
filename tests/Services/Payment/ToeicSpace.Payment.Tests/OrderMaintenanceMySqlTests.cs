using ToeicSpace.Payment.Domain.Entities;
using ToeicSpace.Payment.Domain.Enums;
using ToeicSpace.Payment.Infrastructure.Persistence;
using ToeicSpace.Payment.Infrastructure.Persistence.Repositories;
using ToeicSpace.Payment.Infrastructure.Services.Orders;
using ToeicSpace.Payment.Infrastructure.Services.Content;
using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace ToeicSpace.Payment.Tests;

public sealed class OrderMaintenanceMySqlTests
{
    private static string? Connection => Environment.GetEnvironmentVariable("PBL57_PAYMENT_TEST_DB");
    private static PaymentDbContext Db() => new(new DbContextOptionsBuilder<PaymentDbContext>()
        .UseMySql(Connection!, new MySqlServerVersion(new Version(8, 4, 0))).Options);

    private static PurchaseOrder Order(OrderStatus status, DateTime expiry) => new()
    {
        OrderCode = "ORD" + Guid.NewGuid().ToString("N")[..12].ToUpperInvariant(),
        UserId = Guid.NewGuid(), CourseId = Guid.NewGuid(), CheckoutReference = Guid.NewGuid(),
        PaymentReference = "TS" + Guid.NewGuid().ToString("N")[..12].ToUpperInvariant(),
        OriginalAmount = 100000, FinalAmount = 100000,
        CreatedAt = DateTime.UtcNow.AddMinutes(-20), ExpiresAt = expiry, Status = status,
        PendingKey = status == OrderStatus.Pending ? Guid.NewGuid().ToString("N") : null,
        PaidAt = status == OrderStatus.Paid ? DateTime.UtcNow.AddMinutes(-10) : null,
        Fulfillment = status == OrderStatus.Paid ? FulfillmentStatus.Pending : FulfillmentStatus.NotStarted,
        NextAttemptAt = DateTime.UtcNow.AddMinutes(-1)
    };

    private static OrderMaintenanceRunner Runner(PaymentDbContext db, HttpMessageHandler handler)
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        { ["InternalApi:Key"] = "example-internal-key-at-least-thirty-two-characters" }).Build();
        var client = new ContentCheckoutClient(new HttpClient(handler) { BaseAddress = new Uri("http://localhost/") }, config);
        return new OrderMaintenanceRunner(db, new CouponReservationRepository(db), client,
            NullLogger<OrderMaintenanceRunner>.Instance);
    }

    [Fact]
    public async Task FutureOrder_RemainsPendingAndKeepsPaymentDetails()
    {
        if (Connection is null) return;
        var order = Order(OrderStatus.Pending, DateTime.UtcNow.AddMinutes(13));
        await using var db = Db();
        db.Orders.Add(order);
        await db.SaveChangesAsync();
        await using (var workerDb = Db()) await Runner(workerDb, new Stub(HttpStatusCode.OK)).RunAsync(default);
        await db.Entry(order).ReloadAsync();
        Assert.Equal(OrderStatus.Pending, order.Status);
        Assert.False(order.ReleasePending);
        Assert.Equal(13, (int)Math.Round((order.ExpiresAt - DateTime.UtcNow).TotalMinutes));
    }

    [Fact]
    public async Task ExpiredOrder_ReleasesCouponAndRetriesSlotCleanup()
    {
        if (Connection is null) return;
        await using var db = Db();
        var coupon = new Coupon
        {
            Code = "EXP" + Guid.NewGuid().ToString("N")[..12].ToUpperInvariant(),
            StartAt = DateTime.UtcNow.AddDays(-1), ExpiresAt = DateTime.UtcNow.AddDays(1),
            DiscountType = DiscountType.FixedAmount, DiscountValue = 1000
        };
        var order = Order(OrderStatus.Pending, DateTime.UtcNow.AddMinutes(-1));
        db.Coupons.Add(coupon);
        db.Orders.Add(order);
        db.CouponReservations.Add(new CouponReservation
        {
            CouponId = coupon.Id, OrderId = order.Id, UserId = order.UserId,
            Status = CouponReservationStatus.Reserved, CreatedAt = order.CreatedAt, ExpiresAt = order.ExpiresAt
        });
        await db.SaveChangesAsync();
        var handler = new Stub(HttpStatusCode.ServiceUnavailable);
        await using (var workerDb = Db()) await Runner(workerDb, handler).RunAsync(default);
        await db.Entry(order).ReloadAsync();
        Assert.Equal(OrderStatus.Expired, order.Status);
        Assert.True(order.ReleasePending);
        Assert.Equal(CouponReservationStatus.Released,
            (await db.CouponReservations.AsNoTracking().SingleAsync(x => x.OrderId == order.Id)).Status);
        handler.Status = HttpStatusCode.OK;
        await db.Orders.Where(x => x.Id == order.Id).ExecuteUpdateAsync(s => s.SetProperty(x => x.NextAttemptAt, DateTime.UtcNow.AddSeconds(-1)));
        db.Entry(order).State = EntityState.Detached;
        await using (var workerDb = Db()) await Runner(workerDb, handler).RunAsync(default);
        Assert.False((await db.Orders.SingleAsync(x => x.Id == order.Id)).ReleasePending);
    }

    [Fact]
    public async Task PaidOrder_RetriesAfterContentRecovers()
    {
        if (Connection is null) return;
        await using var db = Db();
        var order = Order(OrderStatus.Paid, DateTime.UtcNow.AddMinutes(1));
        db.Orders.Add(order);
        await db.SaveChangesAsync();
        var handler = new Stub(HttpStatusCode.ServiceUnavailable);
        await using (var workerDb = Db()) await Runner(workerDb, handler).RunAsync(default);
        await db.Entry(order).ReloadAsync();
        Assert.Equal(FulfillmentStatus.Pending, order.Fulfillment);
        Assert.True(order.AttemptCount > 0);
        handler.Status = HttpStatusCode.OK;
        await db.Orders.Where(x => x.Id == order.Id).ExecuteUpdateAsync(s => s.SetProperty(x => x.NextAttemptAt, DateTime.UtcNow.AddSeconds(-1)));
        db.Entry(order).State = EntityState.Detached;
        await using (var workerDb = Db()) await Runner(workerDb, handler).RunAsync(default);
        Assert.Equal(FulfillmentStatus.Completed, (await db.Orders.SingleAsync(x => x.Id == order.Id)).Fulfillment);
    }

    [Fact]
    public async Task PaidPromotion_ConfirmsUsageBeforeFulfillmentAndDoesNotConfirmTwice()
    {
        if (Connection is null) return;
        await using var db = Db();
        var order = Order(OrderStatus.Paid, DateTime.UtcNow.AddMinutes(10));
        order.DiscountSource = OrderDiscountSource.Promotion;
        order.PromotionId = Guid.NewGuid();
        order.DiscountAmount = 10000;
        order.FinalAmount = 90000;
        order.PromotionConfirmPending = true;
        db.Orders.Add(order);
        await db.SaveChangesAsync();
        var handler = new Stub(HttpStatusCode.OK);
        await using (var workerDb = Db()) await Runner(workerDb, handler).RunAsync(default);
        var saved = await db.Orders.AsNoTracking().SingleAsync(x => x.Id == order.Id);
        Assert.False(saved.PromotionConfirmPending);
        Assert.Equal(FulfillmentStatus.Completed, saved.Fulfillment);
        Assert.Equal(1, handler.ConfirmCalls);
        await using (var workerDb = Db()) await Runner(workerDb, handler).RunAsync(default);
        Assert.Equal(1, handler.ConfirmCalls);
    }

    private sealed class Stub(HttpStatusCode status) : HttpMessageHandler
    {
        public HttpStatusCode Status { get; set; } = status;
        public int ConfirmCalls { get; private set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            if (request.RequestUri!.AbsolutePath.EndsWith("/confirm", StringComparison.Ordinal)) ConfirmCalls++;
            return Task.FromResult(new HttpResponseMessage(Status));
        }
    }
}
