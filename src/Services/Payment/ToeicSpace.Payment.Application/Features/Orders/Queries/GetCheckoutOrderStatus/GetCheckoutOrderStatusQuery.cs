using MediatR;
using ToeicSpace.Payment.Application.DTOs.Orders;

namespace ToeicSpace.Payment.Application.Features.Orders.Queries.GetCheckoutOrderStatus;

public sealed record GetCheckoutOrderStatusQuery(Guid CheckoutReference) : IRequest<CheckoutOrderState?>;
