using Microsoft.EntityFrameworkCore;
using ToeicSpace.Payment.Application.DTOs.Orders;
using ToeicSpace.Payment.Application.DTOs.Reconciliation;
using ToeicSpace.Payment.Application.Interfaces.Repositories;
using ToeicSpace.Payment.Domain.Entities;
using ToeicSpace.Payment.Domain.Enums;

namespace ToeicSpace.Payment.Infrastructure.Persistence.Repositories;

public sealed class PaymentReadRepository(PaymentDbContext db) : IPaymentReadRepository
{
    public Task<CheckoutOrderState?> GetCheckoutStatusAsync(Guid reference, CancellationToken ct) =>
        db.Orders.AsNoTracking().Where(o => o.CheckoutReference == reference)
            .Select(o => new CheckoutOrderState(o.Status, o.PaidAt)).SingleOrDefaultAsync(ct);

    public async Task<ReconciliationPage<PaymentTransaction>> GetExceptionsAsync(int page, int pageSize, CancellationToken ct)
    {
        var query = db.Transactions.AsNoTracking().Where(x => x.Status != TransactionStatus.Matched);
        return new ReconciliationPage<PaymentTransaction>(await query.CountAsync(ct),
            await query.OrderByDescending(x => x.ReceivedAt).ThenByDescending(x => x.SePayId)
                .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct));
    }

    public async Task<ReconciliationPage<PurchaseOrder>> GetUnfulfilledAsync(int page, int pageSize, CancellationToken ct)
    {
        var query = db.Orders.AsNoTracking().Where(x => x.Status == OrderStatus.Paid &&
            x.Fulfillment == FulfillmentStatus.NeedsReview);
        return new ReconciliationPage<PurchaseOrder>(await query.CountAsync(ct),
            await query.OrderBy(x => x.PaidAt).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct));
    }
}
