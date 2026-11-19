using ToeicSpace.Payment.Domain.Exceptions;
using ToeicSpace.Payment.Domain.Entities;
using ToeicSpace.Payment.Domain.Enums;
using ToeicSpace.Payment.Infrastructure.Persistence;
using ToeicSpace.Payment.Infrastructure.Persistence.Repositories;
using ToeicSpace.Payment.Infrastructure.Services.Coupons;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ToeicSpace.Payment.Tests;

public sealed class CouponMySqlTests
{
    private static string? Connection => Environment.GetEnvironmentVariable("PBL57_PAYMENT_TEST_DB");
    private static PaymentDbContext Db() => new(new DbContextOptionsBuilder<PaymentDbContext>()
        .UseMySql(Connection!, new MySqlServerVersion(new Version(8, 4, 0))).Options);

    private static async Task<Coupon> MakeCoupon(int? global, int? perUser, decimal discount = 10)
    {
        await using var db = Db();
        var coupon = new Coupon
        {
            Code = "PBL" + Guid.NewGuid().ToString("N")[..12].ToUpperInvariant(),
            StartAt = DateTime.UtcNow.AddMinutes(-5), ExpiresAt = DateTime.UtcNow.AddDays(1),
            DiscountType = DiscountType.Percentage, DiscountValue = discount,
            UsageLimit = global, PerUserLimit = perUser
        };
        coupon.Validate();
        db.Coupons.Add(coupon);
        await db.SaveChangesAsync();
        return coupon;
    }

    private static PurchaseOrder Order(Guid user, Guid course) => new()
    {
        OrderCode = "ORD" + Guid.NewGuid().ToString("N")[..12].ToUpperInvariant(),
        UserId = user, CourseId = course, CheckoutReference = Guid.NewGuid(),
        PaymentReference = "TS" + Guid.NewGuid().ToString("N")[..12].ToUpperInvariant(),
        OriginalAmount = 100000, FinalAmount = 100000,
        CreatedAt = DateTime.UtcNow, ExpiresAt = DateTime.UtcNow.AddMinutes(15)
    };

    [Theory]
    [InlineData(null, true)]
    [InlineData(CouponReservationStatus.Released, true)]
    [InlineData(CouponReservationStatus.Reserved, false)]
    [InlineData(CouponReservationStatus.Redeemed, false)]
    public async Task DeleteCoupon_OnlyRemovesUnusedOrReleasedReservations(
        CouponReservationStatus? status, bool canDelete)
    {
        if (Connection is null) return;
        var coupon = await MakeCoupon(null, null);
        await using var db = Db();
        if (status.HasValue)
        {
            var order = Order(Guid.NewGuid(), Guid.NewGuid());
            db.Orders.Add(order);
            db.CouponReservations.Add(new CouponReservation
            {
                CouponId = coupon.Id, OrderId = order.Id, UserId = order.UserId,
                Status = status.Value, CreatedAt = DateTime.UtcNow, ExpiresAt = order.ExpiresAt
            });
            await db.SaveChangesAsync();
        }

        if (canDelete)
        {
            await new CouponService(db).DeleteAsync(coupon.Id, default);
            Assert.False(await db.Coupons.AnyAsync(x => x.Id == coupon.Id));
            Assert.False(await db.CouponReservations.AnyAsync(x => x.CouponId == coupon.Id));
        }
        else
        {
            var error = await Assert.ThrowsAsync<PaymentException>(() =>
                new CouponService(db).DeleteAsync(coupon.Id, default));
            Assert.Equal("coupon_in_use", error.Code);
            Assert.True(await db.Coupons.AnyAsync(x => x.Id == coupon.Id));
        }
    }

    [Fact]
    public async Task LastCouponUse_OnlyOneConcurrentReservationSucceeds_AndReleaseRestoresIt()
    {
        if (Connection is null) return;
        var coupon = await MakeCoupon(1, 1);
        var course = Guid.NewGuid();
        async Task<PurchaseOrder?> Reserve(Guid user)
        {
            await using var db = Db();
            try { return await new CouponReservationRepository(db).CreateOrderAsync(Order(user, course), coupon.Code, default); }
            catch (PaymentException ex) when (ex.Code == "coupon_limit") { return null; }
        }
        var results = await Task.WhenAll(Reserve(Guid.NewGuid()), Reserve(Guid.NewGuid()));
        var winner = Assert.Single(results, x => x is not null)!;
        await using (var db = Db())
        {
            await using var tx = await db.Database.BeginTransactionAsync();
            await new CouponReservationRepository(db).SettleWithinTransactionAsync(winner.Id, false, default);
            await db.SaveChangesAsync();
            await tx.CommitAsync();
        }
        Assert.NotNull(await Reserve(Guid.NewGuid()));
    }

    [Fact]
    public async Task FullDiscount_PaysWithoutSePayTransactionAndRedeemsOnce()
    {
        if (Connection is null) return;
        var coupon = await MakeCoupon(null, 1, 100);
        await using var db = Db();
        var order = await new CouponReservationRepository(db).CreateOrderAsync(Order(Guid.NewGuid(), Guid.NewGuid()), coupon.Code, default);
        Assert.Equal(OrderStatus.Paid, order.Status);
        Assert.Equal(FulfillmentStatus.Pending, order.Fulfillment);
        Assert.Equal(0, order.FinalAmount);
        Assert.Empty(await db.Transactions.Where(x => x.OrderId == order.Id).ToListAsync());
        Assert.Equal(CouponReservationStatus.Redeemed, (await db.CouponReservations.SingleAsync(x => x.OrderId == order.Id)).Status);
    }

    [Fact]
    public async Task PerUserLimitBlocksRedeemedUserButNotAnotherUser()
    {
        if (Connection is null) return;
        var coupon = await MakeCoupon(null, 1, 100);
        var course = Guid.NewGuid();
        var firstUser = Guid.NewGuid();
        await using var db = Db();
        var store = new CouponReservationRepository(db);
        await store.CreateOrderAsync(Order(firstUser, course), coupon.Code, default);
        await Assert.ThrowsAsync<PaymentException>(() =>
            store.CreateOrderAsync(Order(firstUser, Guid.NewGuid()), coupon.Code, default));
        await using var second = Db();
        var another = await new CouponReservationRepository(second).CreateOrderAsync(
            Order(Guid.NewGuid(), course), coupon.Code, default);
        Assert.Equal(OrderStatus.Paid, another.Status);
    }

    [Fact]
    public async Task AdminCannotLowerPerUserLimitBelowExistingRedemptions()
    {
        if (Connection is null) return;
        var coupon = await MakeCoupon(null, null, 100);
        await using var db = Db();
        var user = Guid.NewGuid();
        var store = new CouponReservationRepository(db);
        await store.CreateOrderAsync(Order(user, Guid.NewGuid()), coupon.Code, default);
        await store.CreateOrderAsync(Order(user, Guid.NewGuid()), coupon.Code, default);
        var edited = new Coupon
        {
            Code = coupon.Code, DiscountType = coupon.DiscountType, DiscountValue = coupon.DiscountValue,
            StartAt = coupon.StartAt, ExpiresAt = coupon.ExpiresAt, PerUserLimit = 1
        };
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new CouponService(db).UpdateAsync(coupon.Id, edited, default));
        Assert.Equal("New limits are below existing usage.", error.Message);
    }

    [Fact]
    public async Task CouponCodeUniqueness_IsCaseInsensitiveInDatabase()
    {
        if (Connection is null) return;
        var first = await MakeCoupon(null, null);
        await using var db = Db();
        var duplicate = new Coupon
        {
            Code = first.Code.ToLowerInvariant(), DiscountType = DiscountType.Percentage,
            DiscountValue = 10, StartAt = DateTime.UtcNow.AddDays(-1),
            ExpiresAt = DateTime.UtcNow.AddDays(1)
        };
        await Assert.ThrowsAsync<DbUpdateException>(() => new CouponService(db).CreateAsync(duplicate, default));
    }
}
