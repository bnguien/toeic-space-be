using MediatR;
using ToeicSpace.Payment.Application.DTOs.Orders;

namespace ToeicSpace.Payment.Application.Features.Orders.Commands.CreateOrder;

public sealed record CreateOrderCommand(Guid CourseId, Guid UserId, Guid CheckoutReference, string? CouponCode)
    : IRequest<OrderResult>;
