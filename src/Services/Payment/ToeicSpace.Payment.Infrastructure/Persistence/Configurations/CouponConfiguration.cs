using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ToeicSpace.Payment.Domain.Entities;

namespace ToeicSpace.Payment.Infrastructure.Persistence.Configurations;

public sealed class CouponConfiguration : IEntityTypeConfiguration<Coupon>
{
    public void Configure(EntityTypeBuilder<Coupon> b)
    {
        b.ToTable("Coupons");
        b.HasKey(x => x.Id);
        b.Property(x => x.Code).HasMaxLength(32).IsRequired();
        b.HasIndex(x => x.Code).IsUnique();
        b.Property(x => x.Description).HasMaxLength(500);
        b.Property(x => x.DiscountType).HasConversion<string>().HasMaxLength(24);
        b.Property(x => x.DiscountValue).HasPrecision(18, 2);
        b.Property(x => x.MinimumOrderAmount).HasPrecision(18, 0);
        b.Property(x => x.MaximumDiscountAmount).HasPrecision(18, 0);
        b.Property(x => x.ApplicableCourseIds)
            .HasConversion(v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                v => JsonSerializer.Deserialize<List<Guid>>(v, (JsonSerializerOptions?)null) ?? new List<Guid>())
            .HasColumnType("json");
        b.Property(x => x.ApplicableCourseIds).Metadata.SetValueComparer(new ValueComparer<List<Guid>>(
            (left, right) => left!.SequenceEqual(right!),
            items => items.Aggregate(0, (hash, id) => HashCode.Combine(hash, id)),
            items => items.ToList()));
    }
}
