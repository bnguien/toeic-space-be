using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ToeicSpace.Content.Domain.Entities;

namespace ToeicSpace.Content.Infrastructure.Persistence.Configurations;

public sealed class CourseEnrollmentConfiguration : IEntityTypeConfiguration<CourseEnrollment>
{
    public void Configure(EntityTypeBuilder<CourseEnrollment> b)
    {
        b.ToTable("CourseEnrollments");
        b.HasKey(x => x.Id);
        b.HasIndex(x => new { x.UserId, x.CourseId }).IsUnique();
        b.HasIndex(x => x.CourseId);
        b.Property(x => x.Status).HasConversion<string>().HasMaxLength(16);
        b.HasOne<Course>().WithMany().HasForeignKey(x => x.CourseId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class CourseSlotReservationConfiguration : IEntityTypeConfiguration<CourseSlotReservation>
{
    public void Configure(EntityTypeBuilder<CourseSlotReservation> b)
    {
        b.ToTable("CourseSlotReservations");
        b.HasKey(x => x.Id);
        b.HasIndex(x => x.CheckoutReference).IsUnique();
        b.HasIndex(x => new { x.CourseId, x.Status });
        b.HasIndex(x => new { x.UserId, x.CourseId, x.Status });
        b.Property(x => x.Status).HasConversion<string>().HasMaxLength(16);
        b.HasOne<Course>().WithMany().HasForeignKey(x => x.CourseId).OnDelete(DeleteBehavior.Restrict);
    }
}
