using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ToeicSpace.Assessment.Domain.Entities;

namespace ToeicSpace.Assessment.Infrastructure.Persistence.Configurations;

public sealed class ToeicPassageConfiguration : IEntityTypeConfiguration<ToeicPassage>
{
    public void Configure(EntityTypeBuilder<ToeicPassage> builder)
    {
        builder.ToTable("ToeicPassages");
        builder.HasKey(x => x.Id);
        builder.HasQueryFilter(x => x.DeletedAt == null);

        builder.Property(x => x.Part)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(x => x.PassageType)
            .HasMaxLength(100);

        builder.Property(x => x.Title)
            .HasMaxLength(500);

        builder.Property(x => x.Content)
            .HasColumnType("longtext");

        builder.Property(x => x.AudioUrl)
            .HasMaxLength(1000);

        builder.Property(x => x.ImageUrl)
            .HasMaxLength(1000);

        builder.Property(x => x.Transcript)
            .HasColumnType("longtext");

        builder.HasIndex(x => new { x.TestId, x.Part, x.OrderIndex });
        builder.HasIndex(x => x.Part);
        builder.HasIndex(x => x.ExternalId);

        // A passage with questions cannot be hard deleted: its questions would lose their context.
        builder.HasMany(x => x.Questions)
            .WithOne(x => x.Passage)
            .HasForeignKey(x => x.PassageId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
