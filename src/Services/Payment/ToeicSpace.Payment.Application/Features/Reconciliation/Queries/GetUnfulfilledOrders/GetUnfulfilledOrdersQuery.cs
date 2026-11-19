using MediatR;
using ToeicSpace.Payment.Application.DTOs.Reconciliation;
using ToeicSpace.Payment.Domain.Entities;

namespace ToeicSpace.Payment.Application.Features.Reconciliation.Queries.GetUnfulfilledOrders;

public sealed record GetUnfulfilledOrdersQuery(int Page, int PageSize)
    : IRequest<ReconciliationPage<PurchaseOrder>>;
