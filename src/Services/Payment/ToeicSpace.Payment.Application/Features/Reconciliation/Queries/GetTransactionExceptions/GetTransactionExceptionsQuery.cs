using MediatR;
using ToeicSpace.Payment.Application.DTOs.Reconciliation;
using ToeicSpace.Payment.Domain.Entities;

namespace ToeicSpace.Payment.Application.Features.Reconciliation.Queries.GetTransactionExceptions;

public sealed record GetTransactionExceptionsQuery(int Page, int PageSize)
    : IRequest<ReconciliationPage<PaymentTransaction>>;
