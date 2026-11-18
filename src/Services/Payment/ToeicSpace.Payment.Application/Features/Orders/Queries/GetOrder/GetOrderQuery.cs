using MediatR;
using ToeicSpace.Payment.Application.DTOs.Orders;

namespace ToeicSpace.Payment.Application.Features.Orders.Queries.GetOrder;

public sealed record GetOrderQuery(Guid Id, Guid UserId) : IRequest<OrderResult>;
