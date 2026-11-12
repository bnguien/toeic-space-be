using System.Text.Json.Serialization;
using ToeicSpace.Payment.Domain.Entities;
using ToeicSpace.Payment.Domain.Enums;

namespace ToeicSpace.Payment.Application.DTOs.Coupons;

public sealed record CouponWithUsage([property: JsonIgnore] Coupon Coupon, int UsedCount, int ReservedCount, int? RemainingUses)
{
    // Flatten the existing coupon fields for admin clients while exposing usage counters.
    public Guid Id => Coupon.Id;
    public string Code => Coupon.Code;
    public string? Description => Coupon.Description;
    public DiscountType DiscountType => Coupon.DiscountType;
    public decimal DiscountValue => Coupon.DiscountValue;
    public decimal MinimumOrderAmount => Coupon.MinimumOrderAmount;
    public decimal? MaximumDiscountAmount => Coupon.MaximumDiscountAmount;
    public DateTime StartAt => Coupon.StartAt;
    public DateTime ExpiresAt => Coupon.ExpiresAt;
    public int? UsageLimit => Coupon.UsageLimit;
    public int? PerUserLimit => Coupon.PerUserLimit;
    public bool IsActive => Coupon.IsActive;
    public IReadOnlyList<Guid> ApplicableCourseIds => Coupon.ApplicableCourseIds;
}

public sealed record CouponPage(int Page, int PageSize, int TotalCount, IReadOnlyList<CouponWithUsage> Items);
