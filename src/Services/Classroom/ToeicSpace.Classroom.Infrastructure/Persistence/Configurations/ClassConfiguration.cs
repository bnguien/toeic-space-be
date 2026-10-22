using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ToeicSpace.Classroom.Domain.Entities;

namespace ToeicSpace.Classroom.Infrastructure.Persistence.Configurations;

public sealed class ClassConfiguration : IEntityTypeConfiguration<Class>
{
    public void Configure(EntityTypeBuilder<Class> b)
    {
        b.ToTable("Classes");
        b.HasKey(x => x.Id);
        b.Property(x => x.Code).HasMaxLength(50).IsRequired();
        b.HasIndex(x => x.Code).IsUnique();
        b.Property(x => x.Name).HasMaxLength(200).IsRequired();
        b.Property(x => x.Description).HasMaxLength(2000);
        b.Property(x => x.Status).HasConversion<string>().HasMaxLength(24).IsRequired();
        b.Property(x => x.ArchivedFromStatus).HasConversion<string>().HasMaxLength(24);
        b.Property(x => x.Version).IsConcurrencyToken();
        b.HasIndex(x => new { x.Status, x.UpdatedAt });
        b.HasIndex(x => x.StartDate);
    }
}

public sealed class ClassInstructorConfiguration : IEntityTypeConfiguration<ClassInstructor>
{
    public void Configure(EntityTypeBuilder<ClassInstructor> b)
    {
        b.ToTable("ClassInstructors");
        b.HasKey(x => x.Id);
        b.HasOne(x => x.Class).WithMany(x => x.Instructors).HasForeignKey(x => x.ClassId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => new { x.ClassId, x.AssignedAt });
        b.HasIndex(x => new { x.TeacherId, x.UnassignedAt });
        // MySQL permits many NULLs in a unique index; generated CurrentClassId is non-null only for current rows.
        b.Property<Guid?>("CurrentClassId")
            .HasColumnType("char(36)")
            .HasComputedColumnSql("CASE WHEN `UnassignedAt` IS NULL THEN `ClassId` ELSE NULL END", stored: true);
        b.HasIndex("CurrentClassId").IsUnique();
    }
}
