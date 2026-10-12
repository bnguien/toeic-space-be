using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ToeicSpace.Assessment.Domain.Entities;

namespace ToeicSpace.Assessment.Infrastructure.Persistence.Configurations;

public sealed class ToeicTestConfiguration : IEntityTypeConfiguration<ToeicTest>
{
    public void Configure(EntityTypeBuilder<ToeicTest> builder)
    {
        builder.ToTable("ToeicTests");
        builder.HasKey(x => x.Id);
        builder.HasQueryFilter(x => x.DeletedAt == null);

        builder.Property(x => x.Title)
            .HasMaxLength(255)
            .IsRequired();

        builder.Property(x => x.Code)
            .HasMaxLength(100)
            .IsRequired();

        builder.HasIndex(x => x.Code)
            .IsUnique();

        builder.Property(x => x.Description)
            .HasMaxLength(2000);

        builder.Property(x => x.Category)
            .HasMaxLength(100);

        builder.Property(x => x.AudioUrl)
            .HasMaxLength(1000);

        builder.Property(x => x.Source)
            .HasMaxLength(100);

        builder.Property(x => x.Status)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(x => x.Metadata)
            .HasColumnType("longtext");

        builder.HasIndex(x => x.ExternalId);
        builder.HasIndex(x => x.Category);
        builder.HasIndex(x => new { x.Status, x.IsActive });

        builder.HasMany(x => x.Passages)
            .WithOne(x => x.Test)
            .HasForeignKey(x => x.TestId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasMany(x => x.Questions)
            .WithOne(x => x.Test)
            .HasForeignKey(x => x.TestId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasMany(x => x.Attempts)
            .WithOne(x => x.Test)
            .HasForeignKey(x => x.TestId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
