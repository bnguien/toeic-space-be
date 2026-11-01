using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ToeicSpace.Content.Domain.Entities;

namespace ToeicSpace.Content.Infrastructure.Persistence.Configurations;

public sealed class LessonConfiguration : IEntityTypeConfiguration<Lesson>
{
    public void Configure(EntityTypeBuilder<Lesson> b)
    {
        b.ToTable("Lessons");
        b.HasKey(x => x.Id);
        b.HasOne(x => x.Module).WithMany(x => x.Lessons).HasForeignKey(x => x.ModuleId)
            .OnDelete(DeleteBehavior.Restrict);
        b.Property(x => x.Title).HasMaxLength(200).IsRequired();
        b.Property(x => x.Description).HasMaxLength(2000);
        b.Property(x => x.TextContent).HasColumnType("longtext");
        b.Property(x => x.Status).HasConversion<string>().HasMaxLength(24).IsRequired();
        b.HasIndex(x => new { x.ModuleId, x.SortOrder }).IsUnique();
    }
}

public sealed class LessonVideoConfiguration : IEntityTypeConfiguration<LessonVideo>
{
    public void Configure(EntityTypeBuilder<LessonVideo> b)
    {
        b.ToTable("LessonVideos");
        b.HasKey(x => x.Id);
        b.HasOne(x => x.Lesson).WithMany(x => x.Videos).HasForeignKey(x => x.LessonId)
            .OnDelete(DeleteBehavior.Cascade);
        b.Property(x => x.FileName).HasMaxLength(255).IsRequired();
        b.Property(x => x.ObjectKey).HasMaxLength(200).IsRequired();
        b.Property(x => x.ContentType).HasMaxLength(100).IsRequired();
        b.HasIndex(x => x.ObjectKey).IsUnique();
        b.HasIndex(x => new { x.LessonId, x.SortOrder }).IsUnique();
    }
}

public sealed class LessonResourceConfiguration : IEntityTypeConfiguration<LessonResource>
{
    public void Configure(EntityTypeBuilder<LessonResource> b)
    {
        b.ToTable("LessonResources");
        b.HasKey(x => x.Id);
        b.HasOne(x => x.Lesson).WithMany(x => x.Resources).HasForeignKey(x => x.LessonId)
            .OnDelete(DeleteBehavior.Cascade);
        b.Property(x => x.FileName).HasMaxLength(255).IsRequired();
        b.Property(x => x.ObjectKey).HasMaxLength(200).IsRequired();
        b.Property(x => x.ContentType).HasMaxLength(100).IsRequired();
        b.HasIndex(x => x.ObjectKey).IsUnique();
    }
}

public sealed class LessonUploadConfiguration : IEntityTypeConfiguration<LessonUpload>
{
    public void Configure(EntityTypeBuilder<LessonUpload> b)
    {
        b.ToTable("LessonUploads");
        b.HasKey(x => x.Key);
        b.Property(x => x.Key).HasMaxLength(200);
        b.Property(x => x.FileName).HasMaxLength(255).IsRequired();
        b.Property(x => x.ContentType).HasMaxLength(100).IsRequired();
        b.Property(x => x.FileSignature).HasMaxLength(64);
        b.Property(x => x.Kind).HasConversion<string>().HasMaxLength(24).IsRequired();
        b.HasIndex(x => new { x.ConfirmedAt, x.ExpiresAt });
    }
}
