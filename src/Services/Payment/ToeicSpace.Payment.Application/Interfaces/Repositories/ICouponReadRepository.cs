using ToeicSpace.Payment.Application.DTOs.Coupons;

namespace ToeicSpace.Payment.Application.Interfaces.Repositories;

public interface ICouponReadRepository
{
    Task<CouponWithUsage?> GetWithUsageAsync(Guid id, CancellationToken ct);
    Task<CouponPage> SearchAsync(string? search, bool? isActive, int page, int pageSize, CancellationToken ct);
}
