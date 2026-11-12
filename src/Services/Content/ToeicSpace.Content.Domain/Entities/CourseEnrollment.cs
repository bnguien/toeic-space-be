namespace ToeicSpace.Content.Domain.Entities;

public enum EnrollmentStatus { Active }
public enum SlotReservationStatus { Reserved, Confirmed, Released }

public sealed class CourseEnrollment
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public Guid CourseId { get; set; }
    public EnrollmentStatus Status { get; set; } = EnrollmentStatus.Active;
    public DateTime EnrolledAt { get; set; }
}

public sealed class CourseSlotReservation
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid CheckoutReference { get; set; }
    public Guid UserId { get; set; }
    public Guid CourseId { get; set; }
    public SlotReservationStatus Status { get; set; } = SlotReservationStatus.Reserved;
    public DateTime ExpiresAt { get; set; }
    public DateTime? ConfirmedAt { get; set; }
}
