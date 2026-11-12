using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ToeicSpace.Payment.Domain.Entities;

namespace ToeicSpace.Payment.Infrastructure.Persistence.Configurations;

public sealed class PaymentTransactionConfiguration : IEntityTypeConfiguration<PaymentTransaction>
{
    public void Configure(EntityTypeBuilder<PaymentTransaction> b)
    {
        b.ToTable("PaymentTransactions");
        b.HasKey(x => x.Id);
        b.HasIndex(x => x.SePayId).IsUnique();
        b.HasIndex(x => x.OrderId);
        b.Property(x => x.Status).HasConversion<string>().HasMaxLength(16);
        b.Property(x => x.PaymentReference).HasMaxLength(20);
        b.Property(x => x.BankReferenceCode).HasMaxLength(128);
        b.Property(x => x.TransferMemo).HasMaxLength(256);
        b.Property(x => x.AccountNumber).HasMaxLength(64);
        b.Property(x => x.Amount).HasPrecision(20, 2);
        b.HasOne<PurchaseOrder>().WithMany().HasForeignKey(x => x.OrderId).OnDelete(DeleteBehavior.Restrict);
    }
}
