using ToeicSpace.Payment.Application.DTOs.SePay;
using ToeicSpace.Payment.Application.Interfaces.Services;
using ToeicSpace.Payment.Domain.Exceptions;
using ToeicSpace.Payment.Domain.Entities;
using ToeicSpace.Payment.Domain.Enums;
using ToeicSpace.Payment.Infrastructure.Persistence;
using ToeicSpace.Payment.Infrastructure.Persistence.Repositories;
using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MySqlConnector;

namespace ToeicSpace.Payment.Infrastructure.Services.SePay;

public sealed partial class SePayWebhookProcessor(PaymentDbContext db, CouponReservationRepository reservations,
    IConfiguration config, Microsoft.Extensions.Logging.ILogger<SePayWebhookProcessor>? log = null) : ISePayWebhookProcessor
{
    [GeneratedRegex("(?<![0-9A-F])TS[0-9A-F]{12}(?![0-9A-F])", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ReferencePattern();

    public async Task ProcessAsync(SePayWebhook webhook, CancellationToken ct)
    {
        if (webhook.Id <= 0) throw new PaymentException("invalid_transaction", "Transaction ID is invalid.", 400);
        try { await ProcessOnceAsync(webhook, ct); }
        catch (DbUpdateException ex) when (ex.InnerException is MySqlException { Number: 1062 })
        {
            db.ChangeTracker.Clear();
            if (!await db.Transactions.AnyAsync(x => x.SePayId == webhook.Id, ct)) throw;
        }
    }

    private async Task ProcessOnceAsync(SePayWebhook webhook, CancellationToken ct)
    {
        var found = ReferencePattern().Matches((webhook.Code ?? "") + " " + (webhook.Content ?? ""))
            .Select(x => x.Value.ToUpperInvariant()).Distinct().ToArray();
        var reference = found.Length == 1 ? found[0] : null;
        var at = DateTime.TryParseExact(webhook.TransactionDate, "yyyy-MM-dd HH:mm:ss",
            CultureInfo.InvariantCulture, DateTimeStyles.None, out var local)
            ? TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(local, DateTimeKind.Unspecified),
                TimeZoneInfo.FindSystemTimeZoneById("Asia/Ho_Chi_Minh")) : DateTime.MinValue;
        var validTransfer = string.Equals(webhook.TransferType, "in", StringComparison.OrdinalIgnoreCase) &&
            webhook.AccountNumber == config["SePay:AccountNumber"] &&
            !string.IsNullOrWhiteSpace(config["SePay:AccountNumber"]) && webhook.TransferAmount > 0 &&
            webhook.TransferAmount == decimal.Truncate(webhook.TransferAmount) && at != DateTime.MinValue &&
            at <= DateTime.UtcNow.AddMinutes(2);

        await using var tx = await db.Database.BeginTransactionAsync(ct);
        if (await db.Transactions.AnyAsync(x => x.SePayId == webhook.Id, ct)) return;
        PurchaseOrder? order = null;
        if (reference is not null)
            order = await db.Orders.FromSqlInterpolated($"SELECT * FROM PurchaseOrders WHERE PaymentReference = {reference} FOR UPDATE")
                .SingleOrDefaultAsync(ct);
        var status = !validTransfer ? TransactionStatus.Invalid :
            found.Length > 1 ? TransactionStatus.Ambiguous :
            order is null ? TransactionStatus.Unmatched :
            webhook.TransferAmount < order.FinalAmount ? TransactionStatus.Underpaid :
            webhook.TransferAmount > order.FinalAmount ? TransactionStatus.Overpaid :
            at < order.CreatedAt.AddMinutes(-2) ? TransactionStatus.Invalid :
            order.Status != OrderStatus.Pending || at > order.ExpiresAt ? TransactionStatus.Late : TransactionStatus.Matched;
        if (status == TransactionStatus.Matched && order is not null)
        {
            await reservations.SettleWithinTransactionAsync(order.Id, true, ct);
            order.Status = OrderStatus.Paid;
            order.PaidAt = at;
            order.ReleasePending = false;
            if (order.DiscountSource == OrderDiscountSource.Promotion) order.PromotionConfirmPending = true;
            order.Fulfillment = FulfillmentStatus.Pending;
            order.NextAttemptAt = DateTime.UtcNow;
        }
        db.Transactions.Add(new PaymentTransaction
        {
            SePayId = webhook.Id, OrderId = order?.Id, PaymentReference = reference,
            BankReferenceCode = webhook.ReferenceCode?[..Math.Min(webhook.ReferenceCode.Length, 128)],
            TransferMemo = webhook.Content?[..Math.Min(webhook.Content.Length, 256)],
            AccountNumber = (webhook.AccountNumber ?? "")[..Math.Min(webhook.AccountNumber?.Length ?? 0, 64)],
            Amount = webhook.TransferAmount,
            TransactionAt = at == DateTime.MinValue ? DateTime.UtcNow : at, ReceivedAt = DateTime.UtcNow, Status = status
        });
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        log?.LogInformation("SePay webhook transaction {SePayId} recorded as {Status} for order {OrderId}.",
            webhook.Id, status, order?.Id);
    }
}
