using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ToeicSpace.Assessment.Domain.Entities;

namespace ToeicSpace.Assessment.Infrastructure.Persistence.Configurations;

public sealed class ToeicPracticeSetConfiguration : IEntityTypeConfiguration<ToeicPracticeSet>
{
    public void Configure(EntityTypeBuilder<ToeicPracticeSet> builder)
    {
        builder.ToTable("ToeicPracticeSets");
        builder.HasKey(x => x.Id);
        builder.HasQueryFilter(x => x.DeletedAt == null);

        builder.Property(x => x.Code)
            .HasMaxLength(100)
            .IsRequired();

        builder.HasIndex(x => x.Code)
            .IsUnique();

        builder.Property(x => x.Title)
            .HasMaxLength(255)
            .IsRequired();

        builder.Property(x => x.Description)
            .HasMaxLength(2000);

        builder.Property(x => x.Kind)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(x => x.Part)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(x => x.Status)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(x => x.Source)
            .HasMaxLength(100);

        builder.HasIndex(x => new { x.Part, x.Kind, x.Status });
        builder.HasIndex(x => x.ExternalId);

        builder.HasMany(x => x.Items)
            .WithOne(x => x.PracticeSet)
            .HasForeignKey(x => x.PracticeSetId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
