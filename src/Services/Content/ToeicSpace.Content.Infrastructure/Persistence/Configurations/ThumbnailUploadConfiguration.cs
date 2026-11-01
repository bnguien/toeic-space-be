using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ToeicSpace.Content.Domain.Entities;

namespace ToeicSpace.Content.Infrastructure.Persistence.Configurations;

public sealed class ThumbnailUploadConfiguration : IEntityTypeConfiguration<ThumbnailUpload>
{
    public void Configure(EntityTypeBuilder<ThumbnailUpload> b)
    {
        b.ToTable("ThumbnailUploads");
        b.HasKey(x => x.Key);
        b.Property(x => x.Key).HasMaxLength(160);
        b.Property(x => x.ContentType).HasMaxLength(32).IsRequired();
        b.HasIndex(x => new { x.ConfirmedAt, x.ExpiresAt });
    }
}
