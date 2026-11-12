using MediatR;
using ToeicSpace.Payment.Application.DTOs.Reconciliation;
using ToeicSpace.Payment.Application.Interfaces.Repositories;
using ToeicSpace.Payment.Domain.Entities;
using ToeicSpace.Payment.Domain.Exceptions;

namespace ToeicSpace.Payment.Application.Features.Reconciliation.Queries.GetTransactionExceptions;

public sealed class GetTransactionExceptionsHandler(IPaymentReadRepository payments)
    : IRequestHandler<GetTransactionExceptionsQuery, ReconciliationPage<PaymentTransaction>>
{
    public Task<ReconciliationPage<PaymentTransaction>> Handle(GetTransactionExceptionsQuery request, CancellationToken ct)
    {
        if (request.Page < 1 || request.PageSize is < 1 or > 100 || request.Page > int.MaxValue / request.PageSize)
            throw new PaymentException("invalid_pagination", "Invalid page or page size.", 400);
        return payments.GetExceptionsAsync(request.Page, request.PageSize, ct);
    }
}
