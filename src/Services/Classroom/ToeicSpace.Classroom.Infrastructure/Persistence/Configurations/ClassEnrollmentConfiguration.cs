using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ToeicSpace.Classroom.Domain.Entities;

namespace ToeicSpace.Classroom.Infrastructure.Persistence.Configurations;

public sealed class ClassEnrollmentConfiguration : IEntityTypeConfiguration<ClassEnrollment>
{
    public void Configure(EntityTypeBuilder<ClassEnrollment> b)
    {
        b.ToTable("ClassEnrollments");
        b.HasKey(x => x.Id);
        b.HasOne(x => x.Class).WithMany(x => x.Enrollments).HasForeignKey(x => x.ClassId).OnDelete(DeleteBehavior.Restrict);
        b.Property(x => x.Status).HasConversion<string>().HasMaxLength(24).IsRequired();
        b.HasIndex(x => new { x.ClassId, x.StudentId, x.EnrolledAt });
        // MySQL allows multiple NULL values; only a current row claims a unique (class, student) slot.
        b.Property<Guid?>("CurrentStudentId").HasColumnType("char(36)")
            .HasComputedColumnSql("CASE WHEN `Status` = 'Active' AND `WithdrawnAt` IS NULL THEN `StudentId` ELSE NULL END", stored: true);
        b.HasIndex("ClassId", "CurrentStudentId").IsUnique();
    }
}
