namespace ToeicSpace.Payment.Application.DTOs.Orders;

public sealed record CourseCheckout(Guid CourseId, decimal Price, string Currency, DateTime? ExpiresAt,
    Guid? ReservationId, bool AlreadyEnrolled);
