using ToeicSpace.Payment.Domain.Enums;

namespace ToeicSpace.Payment.Domain.Entities;

public sealed class CouponReservation
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid CouponId { get; set; }
    public Guid OrderId { get; set; }
    public Guid UserId { get; set; }
    public CouponReservationStatus Status { get; set; }
    public DateTime ExpiresAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? RedeemedAt { get; set; }
}
