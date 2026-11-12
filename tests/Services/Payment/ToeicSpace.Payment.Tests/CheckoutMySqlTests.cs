using ToeicSpace.Payment.Domain.Exceptions;
using ToeicSpace.Payment.Domain.Entities;
using ToeicSpace.Payment.Domain.Enums;
using ToeicSpace.Payment.Infrastructure.Persistence;
using ToeicSpace.Payment.Infrastructure.Persistence.Repositories;
using ToeicSpace.Payment.Infrastructure.Services.Coupons;
using ToeicSpace.Payment.Infrastructure.Services.Orders;
using ToeicSpace.Payment.Infrastructure.Services.Content;
using System.Net;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace ToeicSpace.Payment.Tests;

public sealed class CheckoutMySqlTests
{
    private static string? Connection => Environment.GetEnvironmentVariable("PBL57_PAYMENT_TEST_DB");
    private static PaymentDbContext Db() => new(new DbContextOptionsBuilder<PaymentDbContext>()
        .UseMySql(Connection!, new MySqlServerVersion(new Version(8, 4, 0))).Options);

    private static IConfiguration Config() => new ConfigurationBuilder().AddInMemoryCollection(
        new Dictionary<string, string?>
        {
            ["InternalApi:Key"] = "example-internal-key-at-least-thirty-two-characters",
            ["SePay:Bank"] = "VCB", ["SePay:AccountNumber"] = "0123456789"
        }).Build();

    private static OrderService Service(PaymentDbContext db, CourseStub stub)
    {
        var config = Config();
        return new OrderService(db, new ContentCheckoutClient(new HttpClient(stub)
            { BaseAddress = new Uri("http://localhost/") }, config), new CouponService(db),
            new CouponReservationRepository(db), config);
    }

    [Fact]
    public async Task CouponSearch_PaginatesAndCountsReservedAndRedeemedWithoutReleased()
    {
        if (Connection is null) return;
        await using var db = Db();
        var code = "SEARCH" + Guid.NewGuid().ToString("N")[..10].ToUpperInvariant();
        var coupon = new Coupon
        {
            Code = code, Description = "Searchable offer", DiscountType = DiscountType.FixedAmount,
            DiscountValue = 1000, UsageLimit = 3, StartAt = DateTime.UtcNow.AddDays(-1),
            ExpiresAt = DateTime.UtcNow.AddDays(1)
        };
        db.Coupons.Add(coupon);
        var orders = Enumerable.Range(0, 3).Select(_ => new PurchaseOrder
        {
            OrderCode = "ORD" + Guid.NewGuid().ToString("N")[..12].ToUpperInvariant(),
            UserId = Guid.NewGuid(), CourseId = Guid.NewGuid(), CheckoutReference = Guid.NewGuid(),
            PaymentReference = "TS" + Guid.NewGuid().ToString("N")[..12].ToUpperInvariant(),
            OriginalAmount = 100000, FinalAmount = 99000, CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddMinutes(15)
        }).ToArray();
        db.Orders.AddRange(orders);
        for (var i = 0; i < orders.Length; i++)
            db.CouponReservations.Add(new CouponReservation
            {
                CouponId = coupon.Id, OrderId = orders[i].Id, UserId = orders[i].UserId,
                Status = i switch { 0 => CouponReservationStatus.Reserved,
                    1 => CouponReservationStatus.Redeemed, _ => CouponReservationStatus.Released },
                CreatedAt = DateTime.UtcNow, ExpiresAt = DateTime.UtcNow.AddMinutes(15)
            });
        await db.SaveChangesAsync();
        var repository = new CouponReadRepository(db);
        var page = await repository.SearchAsync(code, true, 1, 20, default);
        Assert.Equal(1, page.TotalCount);
        var item = Assert.Single(page.Items);
        Assert.Equal(1, item.UsedCount);
        Assert.Equal(1, item.ReservedCount);
        Assert.Equal(1, item.RemainingUses);
        var detail = await repository.GetWithUsageAsync(coupon.Id, default);
        Assert.Equal(item.Coupon.Id, detail!.Coupon.Id);
        Assert.Equal(item.UsedCount, detail.UsedCount);
        Assert.Equal(item.ReservedCount, detail.ReservedCount);
        Assert.Empty((await repository.SearchAsync(code, true, 2, 1, default)).Items);
        Assert.Equal(0, (await repository.SearchAsync(code, false, 1, 20, default)).TotalCount);
    }

    [Fact]
    public async Task Checkout_UsesContentPrice_ReturnsMatchingSePayQr_AndRetriesWithoutChangingSnapshot()
    {
        if (Connection is null) return;
        await using var db = Db();
        var courseId = Guid.NewGuid();
        var stub = new CourseStub { Price = 999000, CourseId = courseId };
        var service = Service(db, stub);
        var userId = Guid.NewGuid();
        var reference = Guid.NewGuid();
        var first = await service.CheckoutAsync(courseId, userId, reference, null, default);
        Assert.Equal(999000, first.OriginalAmount);
        Assert.StartsWith("ORD", first.OrderCode);
        Assert.StartsWith("TS", first.PaymentReference);
        Assert.Equal(DateTimeKind.Utc, first.ExpiresAt.Kind);
        Assert.Contains("amount=999000", first.QrUrl);
        Assert.Contains("des=" + first.PaymentReference, first.QrUrl);
        var repeated = await service.CheckoutAsync(courseId, userId, reference, null, default);
        Assert.Equal(first.Id, repeated.Id);
        Assert.Equal(999000, repeated.FinalAmount);
        var secondReference = await service.CheckoutAsync(courseId, userId, Guid.NewGuid(), null, default);
        Assert.Equal(first.Id, secondReference.Id);
        Assert.Single(await db.Orders.Where(x => x.UserId == userId && x.CourseId == courseId).ToListAsync());
        stub.Price = 1000;
        var replacement = await service.CheckoutAsync(courseId, userId, reference, null, default);
        Assert.Equal(first.Id, replacement.Id);
        Assert.Equal(OrderStatus.Pending, replacement.Status);
        Assert.Equal(999000, replacement.OriginalAmount);
        Assert.Contains("amount=999000", replacement.QrUrl);
        Assert.Equal(first.PaymentReference, replacement.PaymentReference);
        Assert.Equal(replacement.Id, (await service.CheckoutAsync(courseId, userId, reference, null, default)).Id);
        Assert.Equal(replacement.Id, (await service.CheckoutAsync(courseId, userId, Guid.NewGuid(), null, default)).Id);
        Assert.Single(await db.Orders.Where(x => x.UserId == userId && x.CourseId == courseId).ToListAsync());
        stub.Price = 2000;
        var refreshedByGet = await service.StatusAsync(replacement.Id, userId, default);
        Assert.Equal(replacement.Id, refreshedByGet.Id);
        Assert.Equal(999000, refreshedByGet.OriginalAmount);
        Assert.Equal(refreshedByGet.Id, (await service.StatusAsync(refreshedByGet.Id, userId, default)).Id);
    }

    [Fact]
    public async Task ContentReservationWithoutTimezone_IsTreatedAsUtcAndReturnsQr()
    {
        if (Connection is null) return;
        await using var db = Db();
        var courseId = Guid.NewGuid();
        var stub = new CourseStub
        {
            Price = 100000, CourseId = courseId,
            ExpiresAtWithoutZone = DateTime.UtcNow.AddMinutes(15).ToString("yyyy-MM-ddTHH:mm:ss.ffffff")
        };
        var result = await Service(db, stub).CheckoutAsync(courseId, Guid.NewGuid(), Guid.NewGuid(), null, default);
        Assert.Equal(OrderStatus.Pending, result.Status);
        Assert.Equal(DateTimeKind.Utc, result.ExpiresAt.Kind);
        Assert.EndsWith("Z", System.Text.Json.JsonSerializer.Serialize(result.ExpiresAt).Trim('"'));
        Assert.InRange((result.ExpiresAt - DateTime.UtcNow).TotalMinutes, 13, 16);
        Assert.NotNull(result.QrUrl);
        Assert.NotNull(result.PaymentReference);
        Assert.NotNull(result.Bank);
        Assert.NotNull(result.AccountNumber);
    }

    [Fact]
    public async Task CancelledOrder_ReleasesCouponAndNewOrderUsesLatestPrice()
    {
        if (Connection is null) return;
        await using var db = Db();
        var coupon = new Coupon
        {
            Code = "REPRICE" + Guid.NewGuid().ToString("N")[..10].ToUpperInvariant(),
            DiscountType = DiscountType.FixedAmount, DiscountValue = 1000,
            StartAt = DateTime.UtcNow.AddDays(-1), ExpiresAt = DateTime.UtcNow.AddDays(1), UsageLimit = 1
        };
        db.Coupons.Add(coupon);
        await db.SaveChangesAsync();
        var courseId = Guid.NewGuid();
        var stub = new CourseStub { CourseId = courseId, Price = 100000 };
        var service = Service(db, stub);
        var user = Guid.NewGuid();
        var first = await service.CheckoutAsync(courseId, user, Guid.NewGuid(), coupon.Code, default);
        stub.Price = 200000;
        Assert.Equal(first.Id, (await service.CheckoutAsync(courseId, user, Guid.NewGuid(), coupon.Code, default)).Id);
        await service.CancelAsync(first.Id, user, default);
        var second = await service.CheckoutAsync(courseId, user, Guid.NewGuid(), coupon.Code, default);
        Assert.Equal(2, stub.Reservations);
        Assert.Equal(199000, second.FinalAmount);
        Assert.Equal(OrderStatus.Cancelled, (await db.Orders.AsNoTracking().SingleAsync(x => x.Id == first.Id)).Status);
        Assert.Equal(CouponReservationStatus.Released,
            (await db.CouponReservations.AsNoTracking().SingleAsync(x => x.OrderId == first.Id)).Status);
        Assert.Equal(CouponReservationStatus.Reserved,
            (await db.CouponReservations.AsNoTracking().SingleAsync(x => x.OrderId == second.Id)).Status);
    }

    [Fact]
    public async Task PendingOrder_DoesNotReleaseSlotWhenPriceChanges()
    {
        if (Connection is null) return;
        await using var db = Db();
        var courseId = Guid.NewGuid();
        var stub = new CourseStub { CourseId = courseId, Price = 100000, RequireReleaseBeforeNextReservation = true };
        var service = Service(db, stub);
        var user = Guid.NewGuid();
        var first = await service.CheckoutAsync(courseId, user, Guid.NewGuid(), null, default);
        stub.Price = 200000;
        var locked = await service.CheckoutAsync(courseId, user, Guid.NewGuid(), null, default);
        Assert.Equal(first.Id, locked.Id);
        Assert.Equal(100000, locked.FinalAmount);
        Assert.Equal(0, stub.Releases);
        Assert.Equal(1, await db.Orders.CountAsync(x => x.UserId == user && x.CourseId == courseId));
    }

    [Fact]
    public async Task FullDiscount_CompletesWithoutQrOrSePayTransaction()
    {
        if (Connection is null) return;
        await using var db = Db();
        var coupon = new Coupon
        {
            Code = "FULL" + Guid.NewGuid().ToString("N")[..10].ToUpperInvariant(),
            DiscountType = DiscountType.Percentage, DiscountValue = 100,
            StartAt = DateTime.UtcNow.AddDays(-1), ExpiresAt = DateTime.UtcNow.AddDays(1)
        };
        db.Coupons.Add(coupon);
        await db.SaveChangesAsync();
        var courseId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var service = Service(db, new CourseStub { Price = 400000, CourseId = courseId });
        var order = await service.CheckoutAsync(courseId, userId, Guid.NewGuid(), coupon.Code, default);
        Assert.Equal(OrderStatus.Paid, order.Status);
        Assert.Equal(0, order.FinalAmount);
        Assert.Null(order.QrUrl);
        Assert.Empty(await db.Transactions.Where(x => x.OrderId == order.Id).ToListAsync());
        var repeatedPurchase = await Assert.ThrowsAsync<PaymentException>(() =>
            service.CheckoutAsync(courseId, userId, Guid.NewGuid(), coupon.Code, default));
        Assert.Equal("already_purchased", repeatedPurchase.Code);
    }

    [Theory]
    [InlineData(30000, "Promotion", 70000)]
    [InlineData(10000, "Coupon", 75000)]
    [InlineData(25000, "Promotion", 75000)]
    public async Task PromotionAndCoupon_NeverStack_AndPromotionWinsTie(decimal promotionDiscount,
        string expectedSource, decimal payable)
    {
        if (Connection is null) return;
        await using var db = Db();
        var code = "COMPARE" + Guid.NewGuid().ToString("N")[..10].ToUpperInvariant();
        db.Coupons.Add(new Coupon { Code = code, DiscountType = DiscountType.FixedAmount,
            DiscountValue = 25000, StartAt = DateTime.UtcNow.AddDays(-1), ExpiresAt = DateTime.UtcNow.AddDays(1) });
        await db.SaveChangesAsync();
        var courseId = Guid.NewGuid();
        var stub = new CourseStub { CourseId = courseId, Price = 100000,
            PromotionId = Guid.NewGuid(), PromotionDiscount = promotionDiscount };
        var service = Service(db, stub);
        var preview = await service.PreviewAsync(courseId, Guid.NewGuid(), code, default);
        Assert.Equal(payable, preview.FinalAmount);
        var user = Guid.NewGuid();
        var order = await service.CheckoutAsync(courseId, user, Guid.NewGuid(), code, default);
        Assert.Equal(expectedSource, order.DiscountSource.ToString());
        Assert.Equal(payable, order.FinalAmount);
        Assert.Equal(expectedSource == "Promotion" ? 1 : 0, stub.PromotionReservations);
        Assert.Equal(expectedSource == "Coupon" ? 1 : 0,
            await db.CouponReservations.CountAsync(x => x.OrderId == order.Id));
        stub.PromotionDiscount = 0; // The order is a snapshot despite a promotion change.
        Assert.Equal(payable, (await service.StatusAsync(order.Id, user, default)).FinalAmount);
    }

    [Fact]
    public async Task FullPromotion_UsesZeroValueFlowWithoutSePayOrCouponUsage()
    {
        if (Connection is null) return;
        await using var db = Db();
        var courseId = Guid.NewGuid();
        var stub = new CourseStub { CourseId = courseId, Price = 100000,
            PromotionId = Guid.NewGuid(), PromotionDiscount = 100000 };
        var order = await Service(db, stub).CheckoutAsync(courseId, Guid.NewGuid(), Guid.NewGuid(), null, default);
        Assert.Equal(OrderStatus.Paid, order.Status);
        Assert.Equal(OrderDiscountSource.Promotion, order.DiscountSource);
        Assert.Equal(0, order.FinalAmount);
        Assert.Null(order.QrUrl);
        Assert.Empty(await db.Transactions.Where(x => x.OrderId == order.Id).ToListAsync());
        Assert.True((await db.Orders.AsNoTracking().SingleAsync(x => x.Id == order.Id)).PromotionConfirmPending);
    }

    [Fact]
    public async Task CancelPromotionOrder_ReleasesOnlyPromotionAndKeepsSnapshot()
    {
        if (Connection is null) return;
        await using var db = Db();
        var courseId = Guid.NewGuid();
        var stub = new CourseStub { CourseId = courseId, Price = 100000,
            PromotionId = Guid.NewGuid(), PromotionDiscount = 5000 };
        var user = Guid.NewGuid();
        var service = Service(db, stub);
        var first = await service.CheckoutAsync(courseId, user, Guid.NewGuid(), null, default);
        Assert.Equal(95000, first.FinalAmount);
        stub.PromotionDiscount = 20000;
        Assert.Equal(first.Id, (await service.CheckoutAsync(courseId, user, Guid.NewGuid(), null, default)).Id);
        await service.CancelAsync(first.Id, user, default);
        Assert.Equal(1, stub.PromotionReleases);
        Assert.Empty(await db.CouponReservations.Where(x => x.OrderId == first.Id).ToListAsync());
        var next = await service.CheckoutAsync(courseId, user, Guid.NewGuid(), null, default);
        Assert.Equal(80000, next.FinalAmount);
    }

    [Fact]
    public async Task PromotionUnavailableDuringReservation_FallsBackToEligibleCoupon()
    {
        if (Connection is null) return;
        await using var db = Db();
        var coupon = new Coupon { Code = "FALLBACK" + Guid.NewGuid().ToString("N")[..10].ToUpperInvariant(),
            DiscountType = DiscountType.FixedAmount, DiscountValue = 2000,
            StartAt = DateTime.UtcNow.AddDays(-1), ExpiresAt = DateTime.UtcNow.AddDays(1) };
        db.Coupons.Add(coupon);
        await db.SaveChangesAsync();
        var courseId = Guid.NewGuid();
        var stub = new CourseStub { CourseId = courseId, Price = 100000,
            PromotionId = Guid.NewGuid(), PromotionDiscount = 5000, FailPromotionReserve = true };
        var order = await Service(db, stub).CheckoutAsync(courseId, Guid.NewGuid(), Guid.NewGuid(), coupon.Code, default);
        Assert.Equal(OrderDiscountSource.Coupon, order.DiscountSource);
        Assert.Equal(98000, order.FinalAmount);
        Assert.Null(order.PromotionId);
        Assert.Single(await db.CouponReservations.Where(x => x.OrderId == order.Id).ToListAsync());
    }

    private sealed class CourseStub : HttpMessageHandler
    {
        public decimal Price { get; set; }
        public Guid CourseId { get; set; }
        public string? ExpiresAtWithoutZone { get; set; }
        public bool RequireReleaseBeforeNextReservation { get; set; }
        public int Reservations { get; private set; }
        public int Releases { get; private set; }
        public bool FailRelease { get; set; }
        public Guid? PromotionId { get; set; }
        public decimal PromotionDiscount { get; set; }
        public int PromotionReservations { get; private set; }
        public int PromotionReleases { get; private set; }
        public bool FailPromotionReserve { get; set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var path = request.RequestUri!.AbsolutePath;
            if (path.Contains("/promotions/", StringComparison.Ordinal))
            {
                if (PromotionId is null) return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NoContent));
                if (path.EndsWith("/promotions/reservations", StringComparison.Ordinal) && FailPromotionReserve)
                    return Task.FromResult(new HttpResponseMessage(HttpStatusCode.Conflict));
                if (path.EndsWith("/release", StringComparison.Ordinal)) PromotionReleases++;
                if (path.EndsWith("/promotions/reservations", StringComparison.Ordinal)) PromotionReservations++;
                var payload = System.Text.Json.JsonSerializer.Serialize(new
                {
                    promotionId = PromotionId, name = "Test promotion", originalPrice = Price,
                    discountAmount = PromotionDiscount, sellingPrice = Price - PromotionDiscount,
                    expiresAt = DateTime.UtcNow.AddHours(1)
                });
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                { Content = new StringContent(payload, Encoding.UTF8, "application/json") });
            }
            if (path.EndsWith("/release", StringComparison.Ordinal) && FailRelease)
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
            if (path.EndsWith("/release", StringComparison.Ordinal)) Releases++;
            if (path.EndsWith("/reservations", StringComparison.Ordinal))
            {
                if (RequireReleaseBeforeNextReservation && Reservations > Releases)
                    return Task.FromResult(new HttpResponseMessage(HttpStatusCode.Conflict));
                Reservations++;
            }
            var result = path.EndsWith("/release", StringComparison.Ordinal)
                ? "{}" : "{\"courseId\":\"" + CourseId + "\",\"price\":" +
                    Price.ToString(System.Globalization.CultureInfo.InvariantCulture) +
                    ",\"currency\":\"VND\",\"expiresAt\":" +
                    (ExpiresAtWithoutZone is null && !RequireReleaseBeforeNextReservation ? "null" : "\"" +
                        (ExpiresAtWithoutZone ?? DateTime.UtcNow.AddMinutes(15).ToString("O")) + "\"") +
                    ",\"reservationId\":" + (ExpiresAtWithoutZone is null && !RequireReleaseBeforeNextReservation ? "null" : "\"" + Guid.NewGuid() + "\"") +
                    ",\"alreadyEnrolled\":false}";
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            { Content = new StringContent(result, Encoding.UTF8, "application/json") });
        }
    }

    [Fact]
    public async Task ConcurrentContentConflict_IsRetryableRatherThanPermanentSlotFailure()
    {
        var client = new ContentCheckoutClient(new HttpClient(new ConflictStub())
        { BaseAddress = new Uri("http://localhost/") }, Config());
        var error = await Assert.ThrowsAsync<PaymentException>(() =>
            client.FulfillAsync(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), DateTime.UtcNow, true, default));
        Assert.Equal(503, error.Status);
    }

    private sealed class ConflictStub : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.Conflict)
            { Content = new StringContent("{\"code\":\"concurrency_conflict\"}", Encoding.UTF8, "application/json") });
    }
}
