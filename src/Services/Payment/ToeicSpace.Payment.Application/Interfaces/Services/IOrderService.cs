using ToeicSpace.Payment.Application.DTOs.Orders;

namespace ToeicSpace.Payment.Application.Interfaces.Services;

public interface IOrderService
{
    Task<PricePreview> PreviewAsync(Guid courseId, Guid userId, string? coupon, CancellationToken ct);
    Task<OrderResult> CheckoutAsync(Guid courseId, Guid userId, Guid reference, string? coupon, CancellationToken ct);
    Task<OrderResult> StatusAsync(Guid id, Guid userId, CancellationToken ct);
    Task CancelAsync(Guid id, Guid userId, CancellationToken ct);
}
