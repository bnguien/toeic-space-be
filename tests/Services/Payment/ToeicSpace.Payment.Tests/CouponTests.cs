using ToeicSpace.Payment.Domain.Entities;
using ToeicSpace.Payment.Domain.Enums;
using Xunit;

namespace ToeicSpace.Payment.Tests;

public sealed class CouponTests
{
    private readonly Guid _courseId = Guid.NewGuid();

    private static Coupon Coupon(DiscountType type, decimal value) => new()
    {
        Code = " save10 ", DiscountType = type, DiscountValue = value,
        StartAt = DateTime.UtcNow.AddDays(-1), ExpiresAt = DateTime.UtcNow.AddDays(1)
    };

    [Fact]
    public void Percentage_RoundsVndAndCapsDiscount()
    {
        var coupon = Coupon(DiscountType.Percentage, 12.5m);
        coupon.MaximumDiscountAmount = 100m;
        coupon.Validate();
        Assert.Equal("SAVE10", coupon.Code);
        Assert.Equal(100m, coupon.CalculateDiscount(_courseId, 999m, DateTime.UtcNow));
    }

    [Fact]
    public void FixedAmount_CannotMakeTotalNegative()
    {
        var coupon = Coupon(DiscountType.FixedAmount, 1000m);
        coupon.Validate();
        Assert.Equal(200m, coupon.CalculateDiscount(_courseId, 200m, DateTime.UtcNow));
    }

    [Fact]
    public void NullableLimitsAreIndependent()
    {
        var coupon = Coupon(DiscountType.Percentage, 10m);
        coupon.PerUserLimit = 1;
        coupon.Validate();
        Assert.Null(coupon.UsageLimit);
        Assert.Equal(1, coupon.PerUserLimit);
        coupon.PerUserLimit = null;
        coupon.UsageLimit = 1;
        coupon.Validate();
        Assert.Null(coupon.PerUserLimit);
    }

    [Theory]
    [InlineData(0, null)]
    [InlineData(null, -1)]
    public void LimitsMustBePositive(int? global, int? perUser)
    {
        var coupon = Coupon(DiscountType.Percentage, 10m);
        coupon.UsageLimit = global;
        coupon.PerUserLimit = perUser;
        Assert.Throws<ArgumentException>(coupon.Validate);
    }

    [Fact]
    public void FreeCourseAndWrongCourseCannotUseCoupon()
    {
        var coupon = Coupon(DiscountType.Percentage, 10m);
        coupon.ApplicableCourseIds.Add(_courseId);
        coupon.Validate();
        Assert.Throws<InvalidOperationException>(() => coupon.CalculateDiscount(_courseId, 0m, DateTime.UtcNow));
        Assert.Throws<InvalidOperationException>(() => coupon.CalculateDiscount(Guid.NewGuid(), 100m, DateTime.UtcNow));
    }
}
