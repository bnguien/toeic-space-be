using ToeicSpace.Payment.Domain.Enums;
using ToeicSpace.BuildingBlocks.Pricing;

namespace ToeicSpace.Payment.Domain.Entities;

public sealed class Coupon
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Code { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DiscountType DiscountType { get; set; }
    public decimal DiscountValue { get; set; }
    public decimal MinimumOrderAmount { get; set; }
    public decimal? MaximumDiscountAmount { get; set; }
    public DateTime StartAt { get; set; }
    public DateTime ExpiresAt { get; set; }
    public int? UsageLimit { get; set; }
    public int? PerUserLimit { get; set; }
    public bool IsActive { get; set; } = true;
    public List<Guid> ApplicableCourseIds { get; set; } = [];

    public void Validate()
    {
        if (Code is null || ApplicableCourseIds is null)
            throw new ArgumentException("Coupon code and applicable course list are required.");
        Code = Code.Trim().ToUpperInvariant();
        if (Code.Length is < 3 or > 32 || !Code.All(c => c is >= 'A' and <= 'Z' or >= '0' and <= '9' or '-' or '_'))
            throw new ArgumentException("Coupon code must be 3–32 ASCII letters, digits, hyphens or underscores.");
        if (Description?.Length > 500 || StartAt >= ExpiresAt || UsageLimit is <= 0 || PerUserLimit is <= 0 ||
            DiscountValue < 0 || DiscountValue > 9999999999999999m ||
            DiscountValue * 100 != decimal.Truncate(DiscountValue * 100) || MinimumOrderAmount < 0 ||
            MinimumOrderAmount > 999999999999999999m || MaximumDiscountAmount is < 0 or > 999999999999999999m ||
            DiscountValue != decimal.Truncate(DiscountValue) && DiscountType == global::ToeicSpace.Payment.Domain.Enums.DiscountType.FixedAmount ||
            MinimumOrderAmount != decimal.Truncate(MinimumOrderAmount) ||
            MaximumDiscountAmount is { } max && max != decimal.Truncate(max) ||
            DiscountType == global::ToeicSpace.Payment.Domain.Enums.DiscountType.Percentage && DiscountValue > 100 ||
            !Enum.IsDefined(DiscountType) || ApplicableCourseIds.Count != ApplicableCourseIds.Distinct().Count())
            throw new ArgumentException("Coupon configuration is invalid.");
    }

    public decimal CalculateDiscount(Guid courseId, decimal amount, DateTime now)
    {
        if (!IsActive || now < StartAt || now >= ExpiresAt || amount <= 0 || amount < MinimumOrderAmount ||
            ApplicableCourseIds.Count != 0 && !ApplicableCourseIds.Contains(courseId))
            throw new InvalidOperationException("Coupon is not eligible for this purchase.");
        return VndDiscount.Calculate(amount, DiscountType == global::ToeicSpace.Payment.Domain.Enums.DiscountType.Percentage,
            DiscountValue, MaximumDiscountAmount);
    }
}
