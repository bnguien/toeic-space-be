using ToeicSpace.Content.Domain.Entities;

namespace ToeicSpace.Content.Application.Interfaces.Services;

public sealed record PromotionPrice(Guid PromotionId, string Name, decimal OriginalPrice,
    decimal DiscountAmount, decimal SellingPrice, DateTime ExpiresAt);
public sealed record PromotionView(CoursePromotion Promotion, int UsedCount, int ReservedCount);
public interface ICoursePromotionService
{
    Task<PromotionView> SaveAsync(Guid? id, CoursePromotion value, CancellationToken ct);
    Task<PromotionPrice> PreviewAsync(Guid courseId, CoursePromotion value, CancellationToken ct);
    Task<PromotionView> ActivateAsync(Guid id, bool active, CancellationToken ct);
    Task DeleteAsync(Guid id, CancellationToken ct);
    Task<IReadOnlyList<PromotionView>> ListAsync(CancellationToken ct);
    Task<IReadOnlyDictionary<Guid, PromotionPrice>> PricesAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct);
    Task<PromotionPrice?> ReserveAsync(Guid courseId, Guid promotionId, Guid reference, decimal expectedPrice,
        DateTime expiresAt, CancellationToken ct);
    Task ConfirmAsync(Guid reference, CancellationToken ct);
    Task ReleaseAsync(Guid reference, CancellationToken ct);
}
