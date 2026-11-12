using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ToeicSpace.Payment.Domain.Entities;

namespace ToeicSpace.Payment.Infrastructure.Persistence.Configurations;

public sealed class CouponReservationConfiguration : IEntityTypeConfiguration<CouponReservation>
{
    public void Configure(EntityTypeBuilder<CouponReservation> b)
    {
        b.ToTable("CouponReservations");
        b.HasKey(x => x.Id);
        b.HasIndex(x => x.OrderId).IsUnique();
        b.HasIndex(x => new { x.CouponId, x.Status });
        b.HasIndex(x => new { x.CouponId, x.UserId, x.Status });
        b.Property(x => x.Status).HasConversion<string>().HasMaxLength(16);
        b.HasOne<Coupon>().WithMany().HasForeignKey(x => x.CouponId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<PurchaseOrder>().WithMany().HasForeignKey(x => x.OrderId).OnDelete(DeleteBehavior.Restrict);
    }
}
