using ToeicSpace.Payment.Domain.Enums;

namespace ToeicSpace.Payment.Application.DTOs.Orders;

public sealed record CheckoutOrderState(OrderStatus Status, DateTime? PaidAt);
