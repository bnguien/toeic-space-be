using ToeicSpace.Payment.Application.DTOs.Orders;
using ToeicSpace.Payment.Application.DTOs.Reconciliation;
using ToeicSpace.Payment.Domain.Entities;

namespace ToeicSpace.Payment.Application.Interfaces.Repositories;

public interface IPaymentReadRepository
{
    Task<CheckoutOrderState?> GetCheckoutStatusAsync(Guid reference, CancellationToken ct);
    Task<ReconciliationPage<PaymentTransaction>> GetExceptionsAsync(int page, int pageSize, CancellationToken ct);
    Task<ReconciliationPage<PurchaseOrder>> GetUnfulfilledAsync(int page, int pageSize, CancellationToken ct);
}
