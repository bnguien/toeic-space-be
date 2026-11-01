using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ToeicSpace.Content.Domain.Entities;

namespace ToeicSpace.Content.Infrastructure.Persistence.Configurations;

public sealed class CourseModuleConfiguration : IEntityTypeConfiguration<CourseModule>
{
    public void Configure(EntityTypeBuilder<CourseModule> b)
    {
        b.ToTable("CourseModules");
        b.HasKey(x => x.Id);
        b.HasOne(x => x.Course).WithMany(x => x.Modules).HasForeignKey(x => x.CourseId)
            .OnDelete(DeleteBehavior.Restrict);
        b.Property(x => x.Title).HasMaxLength(200).IsRequired();
        b.Property(x => x.Description).HasMaxLength(2000);
        b.HasIndex(x => new { x.CourseId, x.SortOrder }).IsUnique();
    }
}
