using ToeicSpace.Payment.Application.DTOs.SePay;
using ToeicSpace.Payment.Domain.Entities;
using ToeicSpace.Payment.Domain.Enums;
using ToeicSpace.Payment.Infrastructure.Persistence;
using ToeicSpace.Payment.Infrastructure.Persistence.Repositories;
using ToeicSpace.Payment.Infrastructure.Services.SePay;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace ToeicSpace.Payment.Tests;

public sealed class SePayMySqlTests
{
    private static string? Connection => Environment.GetEnvironmentVariable("PBL57_PAYMENT_TEST_DB");
    private static PaymentDbContext Db() => new(new DbContextOptionsBuilder<PaymentDbContext>()
        .UseMySql(Connection!, new MySqlServerVersion(new Version(8, 4, 0))).Options);
    private static IConfiguration Config() => new ConfigurationBuilder().AddInMemoryCollection(
        new Dictionary<string, string?> { ["SePay:AccountNumber"] = "0123456789" }).Build();
    private static long Id() => (long)Random.Shared.Next(1, int.MaxValue) * 10000 + Random.Shared.Next(10000);
    private static string BankTime(DateTime utc) => TimeZoneInfo.ConvertTimeFromUtc(utc,
        TimeZoneInfo.FindSystemTimeZoneById("Asia/Ho_Chi_Minh")).ToString("yyyy-MM-dd HH:mm:ss");

    [Fact]
    public async Task VerifiedWebhook_PaysOnce_AndRejectsWrongAmount()
    {
        if (Connection is null) return;
        await using var db = Db();
        var order = new PurchaseOrder
        {
            OrderCode = "ORD" + Guid.NewGuid().ToString("N")[..12].ToUpperInvariant(),
            UserId = Guid.NewGuid(), CourseId = Guid.NewGuid(), CheckoutReference = Guid.NewGuid(),
            PaymentReference = "TS" + Guid.NewGuid().ToString("N")[..12].ToUpperInvariant(),
            OriginalAmount = 100000, FinalAmount = 100000,
            CreatedAt = DateTime.UtcNow.AddMinutes(-2), ExpiresAt = DateTime.UtcNow.AddMinutes(10)
        };
        db.Orders.Add(order);
        await db.SaveChangesAsync();
        var processor = new SePayWebhookProcessor(db, new CouponReservationRepository(db), Config());
        var underpaid = new SePayWebhook(Id(), "VCB", BankTime(DateTime.UtcNow), "0123456789",
            order.PaymentReference, order.PaymentReference, "in", 50000, null);
        await processor.ProcessAsync(underpaid, default);
        Assert.Equal(OrderStatus.Pending, (await db.Orders.SingleAsync(x => x.Id == order.Id)).Status);
        Assert.Equal(TransactionStatus.Underpaid, (await db.Transactions.SingleAsync(x => x.SePayId == underpaid.Id)).Status);
        var verified = underpaid with { Id = Id(), TransferAmount = 100000 };
        await processor.ProcessAsync(verified, default);
        await processor.ProcessAsync(verified, default);
        await db.Entry(order).ReloadAsync();
        Assert.Equal(OrderStatus.Paid, order.Status);
        Assert.Equal(FulfillmentStatus.Pending, order.Fulfillment);
        Assert.Single(await db.Transactions.Where(x => x.SePayId == verified.Id).ToListAsync());
        Assert.Equal(TransactionStatus.Matched, (await db.Transactions.SingleAsync(x => x.SePayId == verified.Id)).Status);
    }

    [Fact]
    public async Task LateAndUnmatchedPayments_AreRecordedWithoutEnrollmentFulfillment()
    {
        if (Connection is null) return;
        await using var db = Db();
        var order = new PurchaseOrder
        {
            OrderCode = "ORD" + Guid.NewGuid().ToString("N")[..12].ToUpperInvariant(),
            UserId = Guid.NewGuid(), CourseId = Guid.NewGuid(), CheckoutReference = Guid.NewGuid(),
            PaymentReference = "TS" + Guid.NewGuid().ToString("N")[..12].ToUpperInvariant(),
            OriginalAmount = 100000, FinalAmount = 100000, Status = OrderStatus.Expired,
            CreatedAt = DateTime.UtcNow.AddMinutes(-30), ExpiresAt = DateTime.UtcNow.AddMinutes(-15)
        };
        db.Orders.Add(order);
        await db.SaveChangesAsync();
        var processor = new SePayWebhookProcessor(db, new CouponReservationRepository(db), Config());
        var late = new SePayWebhook(Id(), "VCB", BankTime(DateTime.UtcNow), "0123456789",
            order.PaymentReference, null, "in", 100000, null);
        await processor.ProcessAsync(late, default);
        Assert.Equal(TransactionStatus.Late, (await db.Transactions.SingleAsync(x => x.SePayId == late.Id)).Status);
        Assert.Equal(OrderStatus.Expired, (await db.Orders.SingleAsync(x => x.Id == order.Id)).Status);
        var unmatched = late with { Id = Id(), Content = "unmatched bank memo", Code = "no reference", ReferenceCode = "FT-test" };
        await processor.ProcessAsync(unmatched, default);
        var recorded = await db.Transactions.SingleAsync(x => x.SePayId == unmatched.Id);
        Assert.Equal(TransactionStatus.Unmatched, recorded.Status);
        Assert.Equal("unmatched bank memo", recorded.TransferMemo);
        Assert.Equal("FT-test", recorded.BankReferenceCode);
    }

    [Fact]
    public async Task AmbiguousReferenceAndWrongAccount_NeverPay()
    {
        if (Connection is null) return;
        await using var db = Db();
        var first = new PurchaseOrder
        {
            OrderCode = "ORD" + Guid.NewGuid().ToString("N")[..12].ToUpperInvariant(),
            UserId = Guid.NewGuid(), CourseId = Guid.NewGuid(), CheckoutReference = Guid.NewGuid(),
            PaymentReference = "TS" + Guid.NewGuid().ToString("N")[..12].ToUpperInvariant(),
            OriginalAmount = 100000, FinalAmount = 100000,
            CreatedAt = DateTime.UtcNow.AddMinutes(-2), ExpiresAt = DateTime.UtcNow.AddMinutes(10)
        };
        db.Orders.Add(first);
        await db.SaveChangesAsync();
        var processor = new SePayWebhookProcessor(db, new CouponReservationRepository(db), Config());
        var badAccount = new SePayWebhook(Id(), "VCB", BankTime(DateTime.UtcNow), "not-our-account",
            first.PaymentReference, null, "in", 100000, null);
        await processor.ProcessAsync(badAccount, default);
        Assert.Equal(TransactionStatus.Invalid, (await db.Transactions.SingleAsync(x => x.SePayId == badAccount.Id)).Status);
        var other = "TS" + Guid.NewGuid().ToString("N")[..12].ToUpperInvariant();
        var ambiguous = badAccount with { Id = Id(), AccountNumber = "0123456789", Content = other };
        await processor.ProcessAsync(ambiguous, default);
        Assert.Equal(TransactionStatus.Ambiguous, (await db.Transactions.SingleAsync(x => x.SePayId == ambiguous.Id)).Status);
        Assert.Equal(OrderStatus.Pending, (await db.Orders.SingleAsync(x => x.Id == first.Id)).Status);
    }

    [Fact]
    public async Task BankTransferBeforeDeadline_CanBeVerifiedAfterDeadlineBeforeSlotRelease()
    {
        if (Connection is null) return;
        await using var db = Db();
        var order = new PurchaseOrder
        {
            OrderCode = "ORD" + Guid.NewGuid().ToString("N")[..12].ToUpperInvariant(),
            UserId = Guid.NewGuid(), CourseId = Guid.NewGuid(), CheckoutReference = Guid.NewGuid(),
            PaymentReference = "TS" + Guid.NewGuid().ToString("N")[..12].ToUpperInvariant(),
            OriginalAmount = 100000, FinalAmount = 100000,
            CreatedAt = DateTime.UtcNow.AddMinutes(-15), ExpiresAt = DateTime.UtcNow.AddMinutes(-1)
        };
        db.Orders.Add(order);
        await db.SaveChangesAsync();
        var transfer = new SePayWebhook(Id(), "VCB", BankTime(DateTime.UtcNow.AddMinutes(-2)),
            "0123456789", order.PaymentReference, null, "in", 100000, null);
        await new SePayWebhookProcessor(db, new CouponReservationRepository(db), Config()).ProcessAsync(transfer, default);
        await db.Entry(order).ReloadAsync();
        Assert.Equal(OrderStatus.Paid, order.Status);
        Assert.Equal(FulfillmentStatus.Pending, order.Fulfillment);
    }
}
