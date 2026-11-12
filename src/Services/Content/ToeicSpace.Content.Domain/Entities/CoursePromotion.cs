using ToeicSpace.BuildingBlocks.Pricing;

namespace ToeicSpace.Content.Domain.Entities;

public enum PromotionDiscountType { Percentage = 1, FixedAmount = 2 }
public enum PromotionReservationStatus { Reserved = 1, Redeemed = 2, Released = 3 }

public sealed class CoursePromotion
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public PromotionDiscountType DiscountType { get; set; }
    public decimal DiscountValue { get; set; }
    public decimal? MaximumDiscountAmount { get; set; }
    public DateTime StartAt { get; set; }
    public DateTime ExpiresAt { get; set; }
    public bool IsActive { get; set; } = true;
    public int? UsageLimit { get; set; }
    public List<Guid> CourseIds { get; set; } = [];

    public decimal Discount(decimal price) => VndDiscount.Calculate(price,
        DiscountType == PromotionDiscountType.Percentage, DiscountValue, MaximumDiscountAmount);
}

public sealed class PromotionReservation
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid PromotionId { get; set; }
    public Guid CheckoutReference { get; set; }
    public Guid CourseId { get; set; }
    public decimal OriginalPrice { get; set; }
    public decimal DiscountAmount { get; set; }
    public PromotionReservationStatus Status { get; set; } = PromotionReservationStatus.Reserved;
    public DateTime CreatedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
}
