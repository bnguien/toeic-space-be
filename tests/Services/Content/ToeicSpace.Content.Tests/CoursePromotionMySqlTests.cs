using Microsoft.EntityFrameworkCore;
using ToeicSpace.Content.Domain.Entities;
using ToeicSpace.Content.Domain.Exceptions;
using ToeicSpace.Content.Infrastructure.Persistence;
using ToeicSpace.Content.Infrastructure.Services;
using Xunit;

namespace ToeicSpace.Content.Tests;

public sealed class CoursePromotionMySqlTests
{
    private static string? Connection => Environment.GetEnvironmentVariable("PBL57_CONTENT_TEST_DB");
    private static ContentDbContext Db() => new(new DbContextOptionsBuilder<ContentDbContext>()
        .UseMySql(Connection!, new MySqlServerVersion(new Version(8, 4, 0))).Options);
    private static CoursePromotionService Service(ContentDbContext db) => new(db, TimeProvider.System);

    [Theory]
    [InlineData(null, true)]
    [InlineData(PromotionReservationStatus.Released, true)]
    [InlineData(PromotionReservationStatus.Reserved, false)]
    [InlineData(PromotionReservationStatus.Redeemed, false)]
    public async Task DeletePromotion_OnlyRemovesUnusedOrReleasedReservations(
        PromotionReservationStatus? status, bool canDelete)
    {
        if (Connection is null) return;
        await using var db = Db();
        var courseId = Guid.NewGuid();
        db.Courses.Add(new Course
        {
            Id = courseId, Code = courseId.ToString("N"), Slug = "delete-" + courseId.ToString("N"),
            Title = "Promotion delete test", Price = 100000
        });
        await db.SaveChangesAsync();
        var promotion = new CoursePromotion
        {
            Name = "Delete test", DiscountType = PromotionDiscountType.FixedAmount,
            DiscountValue = 1000, CourseIds = [courseId], IsActive = false,
            StartAt = DateTime.UtcNow.AddDays(-1), ExpiresAt = DateTime.UtcNow.AddDays(1)
        };
        await Service(db).SaveAsync(null, promotion, default);
        if (status.HasValue)
        {
            db.PromotionReservations.Add(new PromotionReservation
            {
                PromotionId = promotion.Id, CheckoutReference = Guid.NewGuid(), CourseId = courseId,
                OriginalPrice = 100000, DiscountAmount = 1000, Status = status.Value,
                CreatedAt = DateTime.UtcNow, ExpiresAt = DateTime.UtcNow.AddMinutes(15)
            });
            await db.SaveChangesAsync();
        }

        if (canDelete)
        {
            await Service(db).DeleteAsync(promotion.Id, default);
            Assert.False(await db.CoursePromotions.AnyAsync(x => x.Id == promotion.Id));
            Assert.False(await db.PromotionReservations.AnyAsync(x => x.PromotionId == promotion.Id));
        }
        else
        {
            var error = await Assert.ThrowsAsync<ContentRuleException>(() =>
                Service(db).DeleteAsync(promotion.Id, default));
            Assert.Equal("promotion_in_use", error.Code);
            Assert.True(await db.CoursePromotions.AnyAsync(x => x.Id == promotion.Id));
        }
    }

    [Fact]
    public async Task ConcurrentAdminSchedules_CannotOverlap()
    {
        if (Connection is null) return;
        await using var setup = Db();
        var id = Guid.NewGuid();
        setup.Courses.Add(new Course { Id = id, Code = id.ToString("N"), Slug = "overlap-" + id.ToString("N"),
            Title = "Overlap test", Price = 100000 });
        await setup.SaveChangesAsync();
        async Task<bool> Create()
        {
            await using var db = Db();
            try
            {
                await Service(db).SaveAsync(null, new CoursePromotion { Name = "Concurrent",
                    DiscountType = PromotionDiscountType.FixedAmount, DiscountValue = 1000,
                    CourseIds = [id], StartAt = DateTime.UtcNow.AddMinutes(1),
                    ExpiresAt = DateTime.UtcNow.AddHours(1) }, default);
                return true;
            }
            catch (ContentRuleException ex) when (ex.Code == "promotion_overlap") { return false; }
        }
        Assert.Single(await Task.WhenAll(Create(), Create()), success => success);
    }

    [Fact]
    public async Task ConcurrentReservations_OnlyOneCanUseLastPromotion()
    {
        if (Connection is null) return;
        await using var setup = Db();
        var id = Guid.NewGuid();
        setup.Courses.Add(new Course { Id = id, Code = id.ToString("N"), Slug = "limited-" + id.ToString("N"),
            Title = "Limited promotion", Price = 100000 });
        await setup.SaveChangesAsync();
        var p = new CoursePromotion { Name = "Last use", DiscountType = PromotionDiscountType.FixedAmount,
            DiscountValue = 5000, CourseIds = [id], UsageLimit = 1,
            StartAt = DateTime.UtcNow.AddMinutes(-1), ExpiresAt = DateTime.UtcNow.AddMinutes(20) };
        await Service(setup).SaveAsync(null, p, default);
        async Task<bool> Reserve()
        {
            await using var db = Db();
            try { await Service(db).ReserveAsync(id, p.Id, Guid.NewGuid(), 100000,
                DateTime.UtcNow.AddMinutes(15), default); return true; }
            catch (ContentRuleException ex) when (ex.Code is "promotion_unavailable" or "concurrency_conflict")
            { return false; }
        }
        var results = await Task.WhenAll(Reserve(), Reserve());
        Assert.Single(results, value => value);
        Assert.Equal(1, await setup.PromotionReservations.CountAsync(x => x.PromotionId == p.Id &&
            x.Status == PromotionReservationStatus.Reserved));
    }

    [Fact]
    public async Task HalfOpenSchedule_UsageLimit_AndIdempotentSettlement()
    {
        if (Connection is null) return;
        await using var db = Db();
        var id = Guid.NewGuid();
        db.Courses.Add(new Course
        {
            Id = id, Code = id.ToString("N"), Slug = "promo-" + id.ToString("N"), Title = "Promo test",
            Status = ToeicSpace.Content.Domain.Enums.CourseStatus.Published, Price = 100005,
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();
        var now = DateTime.UtcNow;
        var promo = new CoursePromotion { Name = "Ten percent", DiscountType = PromotionDiscountType.Percentage,
            DiscountValue = 10, CourseIds = [id], UsageLimit = 1,
            StartAt = now.AddMinutes(-2), ExpiresAt = now.AddMinutes(10) };
        await Service(db).SaveAsync(null, promo, default);
        var adjacent = new CoursePromotion { Name = "Next", DiscountType = PromotionDiscountType.FixedAmount,
            DiscountValue = 1500, CourseIds = [id], StartAt = promo.ExpiresAt,
            ExpiresAt = promo.ExpiresAt.AddHours(1) };
        await Service(db).SaveAsync(null, adjacent, default);
        Assert.Equal(1500, adjacent.Discount(100005));
        var capped = new CoursePromotion { DiscountType = PromotionDiscountType.Percentage, DiscountValue = 50,
            MaximumDiscountAmount = 500, CourseIds = [id] };
        Assert.Equal(500, capped.Discount(100005));
        Assert.Equal("promotion_overlap", (await Assert.ThrowsAsync<ContentRuleException>(() =>
            Service(db).SaveAsync(null, WithClone(promo.ExpiresAt.AddSeconds(-1)), default))).Code);
        var quote = (await Service(db).PricesAsync([id], default))[id];
        Assert.Equal(10001, quote.DiscountAmount); // Half VND rounds away from zero.
        var reference = Guid.NewGuid();
        var held = await Service(db).ReserveAsync(id, promo.Id, reference, 100005, now.AddMinutes(9), default);
        Assert.Equal(quote.DiscountAmount, held!.DiscountAmount);
        Assert.False((await Service(db).PricesAsync([id], default)).ContainsKey(id));
        await Service(db).ReleaseAsync(reference, default);
        await Service(db).ReleaseAsync(reference, default);
        var next = Guid.NewGuid();
        await Service(db).ReserveAsync(id, promo.Id, next, 100005, now.AddMinutes(9), default);
        await Service(db).ActivateAsync(promo.Id, false, default);
        Assert.False((await Service(db).PricesAsync([id], default)).ContainsKey(id));
        await Service(db).ConfirmAsync(next, default);
        await Service(db).ConfirmAsync(next, default);
        Assert.Equal(PromotionReservationStatus.Redeemed,
            (await db.PromotionReservations.AsNoTracking().SingleAsync(x => x.CheckoutReference == next)).Status);

        CoursePromotion WithClone(DateTime start) => new()
        {
            Name = "Overlap", DiscountType = PromotionDiscountType.FixedAmount, DiscountValue = 100,
            CourseIds = [id], StartAt = start, ExpiresAt = start.AddHours(1)
        };
    }
}
