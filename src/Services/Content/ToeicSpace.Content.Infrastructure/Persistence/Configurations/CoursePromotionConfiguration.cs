using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ToeicSpace.Content.Domain.Entities;

namespace ToeicSpace.Content.Infrastructure.Persistence.Configurations;

public sealed class CoursePromotionConfiguration : IEntityTypeConfiguration<CoursePromotion>
{
    public void Configure(EntityTypeBuilder<CoursePromotion> b)
    {
        b.ToTable("CoursePromotions");
        b.HasKey(x => x.Id);
        b.Property(x => x.Name).HasMaxLength(160).IsRequired();
        b.Property(x => x.DiscountType).HasConversion<string>().HasMaxLength(20);
        b.Property(x => x.DiscountValue).HasPrecision(18, 2);
        b.Property(x => x.MaximumDiscountAmount).HasPrecision(18, 0);
        b.Property(x => x.CourseIds).HasConversion(
            x => JsonSerializer.Serialize(x, (JsonSerializerOptions?)null),
            x => JsonSerializer.Deserialize<List<Guid>>(x, (JsonSerializerOptions?)null) ?? new List<Guid>())
            .HasColumnType("json");
        b.HasIndex(x => new { x.IsActive, x.StartAt, x.ExpiresAt });
    }
}

public sealed class PromotionReservationConfiguration : IEntityTypeConfiguration<PromotionReservation>
{
    public void Configure(EntityTypeBuilder<PromotionReservation> b)
    {
        b.ToTable("PromotionReservations");
        b.HasKey(x => x.Id);
        b.HasIndex(x => x.CheckoutReference).IsUnique();
        b.HasIndex(x => new { x.PromotionId, x.Status });
        b.Property(x => x.OriginalPrice).HasPrecision(18, 0);
        b.Property(x => x.DiscountAmount).HasPrecision(18, 0);
        b.Property(x => x.Status).HasConversion<string>().HasMaxLength(16);
        b.HasOne<CoursePromotion>().WithMany().HasForeignKey(x => x.PromotionId).OnDelete(DeleteBehavior.Restrict);
    }
}
