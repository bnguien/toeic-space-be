using ToeicSpace.Payment.Domain.Entities;

namespace ToeicSpace.Payment.Application.Interfaces.Services;

public interface ICouponService
{
    Task<Coupon> CreateAsync(Coupon coupon, CancellationToken ct);
    Task<Coupon?> GetAsync(Guid id, CancellationToken ct);
    Task<IReadOnlyList<Coupon>> ListAsync(CancellationToken ct);
    Task<Coupon> UpdateAsync(Guid id, Coupon values, CancellationToken ct);
    Task<Coupon> DeactivateAsync(Guid id, CancellationToken ct);
    Task DeleteAsync(Guid id, CancellationToken ct);
    Task<decimal> PreviewAsync(string code, Guid courseId, decimal amount, Guid userId, CancellationToken ct);
}
