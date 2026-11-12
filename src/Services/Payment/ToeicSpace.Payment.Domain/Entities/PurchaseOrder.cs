using ToeicSpace.Payment.Domain.Enums;

namespace ToeicSpace.Payment.Domain.Entities;

public sealed class PurchaseOrder
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string OrderCode { get; set; } = string.Empty;
    public Guid CheckoutReference { get; set; }
    // Unique while the order is pending or paid; released only for cancelled/expired orders.
    public string? PendingKey { get; set; }
    public string PaymentReference { get; set; } = string.Empty;
    public Guid UserId { get; set; }
    public Guid CourseId { get; set; }
    public Guid? SlotReservationId { get; set; }
    public decimal OriginalAmount { get; set; }
    public string? CouponCode { get; set; }
    public string? RequestedCouponCode { get; set; }
    public OrderDiscountSource DiscountSource { get; set; }
    public Guid? PromotionId { get; set; }
    public string? PromotionName { get; set; }
    public DateTime? PromotionExpiresAt { get; set; }
    public bool PromotionReleasePending { get; set; }
    public bool PromotionConfirmPending { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal FinalAmount { get; set; }
    public string Currency { get; set; } = "VND";
    public OrderStatus Status { get; set; } = OrderStatus.Pending;
    public FulfillmentStatus Fulfillment { get; set; } = FulfillmentStatus.NotStarted;
    public bool ReleasePending { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
    public DateTime? PaidAt { get; set; }
    public DateTime? NextAttemptAt { get; set; }
    public int AttemptCount { get; set; }
}
