using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ToeicSpace.Payment.Domain.Entities;

namespace ToeicSpace.Payment.Infrastructure.Persistence.Configurations;

public sealed class PurchaseOrderConfiguration : IEntityTypeConfiguration<PurchaseOrder>
{
    public void Configure(EntityTypeBuilder<PurchaseOrder> b)
    {
        b.ToTable("PurchaseOrders");
        b.HasKey(x => x.Id);
        b.HasIndex(x => x.CheckoutReference).IsUnique();
        b.HasIndex(x => x.OrderCode).IsUnique();
        b.Property(x => x.OrderCode).HasMaxLength(20).IsRequired();
        b.HasIndex(x => x.PendingKey).IsUnique();
        b.Property(x => x.PendingKey).HasMaxLength(65);
        b.HasIndex(x => x.PaymentReference).IsUnique();
        b.HasIndex(x => new { x.UserId, x.CourseId, x.Status });
        b.Property(x => x.PaymentReference).HasMaxLength(20).IsRequired();
        b.Property(x => x.CouponCode).HasMaxLength(32);
        b.Property(x => x.RequestedCouponCode).HasMaxLength(32);
        b.Property(x => x.PromotionName).HasMaxLength(160);
        b.Property(x => x.DiscountSource).HasConversion<string>().HasMaxLength(16);
        b.Property(x => x.Currency).HasMaxLength(3).IsRequired();
        b.Property(x => x.Status).HasConversion<string>().HasMaxLength(16);
        b.Property(x => x.Fulfillment).HasConversion<string>().HasMaxLength(16);
        b.Property(x => x.OriginalAmount).HasPrecision(18, 0);
        b.Property(x => x.DiscountAmount).HasPrecision(18, 0);
        b.Property(x => x.FinalAmount).HasPrecision(18, 0);
    }
}
