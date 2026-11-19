namespace ToeicSpace.Payment.Application.DTOs.Orders;

public sealed record PromotionQuote(Guid PromotionId, string Name, decimal OriginalPrice,
    decimal DiscountAmount, decimal SellingPrice, DateTime ExpiresAt);
