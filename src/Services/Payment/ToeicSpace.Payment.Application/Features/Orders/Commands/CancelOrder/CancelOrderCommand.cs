using MediatR;

namespace ToeicSpace.Payment.Application.Features.Orders.Commands.CancelOrder;

public sealed record CancelOrderCommand(Guid Id, Guid UserId) : IRequest;
