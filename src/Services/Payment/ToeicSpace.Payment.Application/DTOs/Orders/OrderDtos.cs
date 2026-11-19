using ToeicSpace.Payment.Domain.Enums;

namespace ToeicSpace.Payment.Application.DTOs.Orders;

public sealed record PricePreview(Guid CourseId, decimal OriginalAmount, decimal DiscountAmount,
    decimal FinalAmount, string Currency, OrderDiscountSource DiscountSource = OrderDiscountSource.None,
    Guid? PromotionId = null, string? PromotionName = null, DateTime? PromotionExpiresAt = null);
public sealed record OrderResult(Guid Id, string OrderCode, Guid CourseId, OrderStatus Status, FulfillmentStatus Fulfillment,
    decimal OriginalAmount, string? CouponCode, decimal DiscountAmount, decimal FinalAmount, string Currency,
    DateTime ExpiresAt, string? PaymentReference, string? Bank, string? AccountNumber, string? QrUrl,
    OrderDiscountSource DiscountSource = OrderDiscountSource.None, Guid? PromotionId = null,
    string? PromotionName = null, DateTime? PromotionExpiresAt = null);
