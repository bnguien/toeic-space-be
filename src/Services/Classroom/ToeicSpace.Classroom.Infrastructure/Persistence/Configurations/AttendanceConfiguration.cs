using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ToeicSpace.Classroom.Domain.Entities;

namespace ToeicSpace.Classroom.Infrastructure.Persistence.Configurations;

public sealed class AttendanceConfiguration : IEntityTypeConfiguration<AttendanceRecord>
{
    public void Configure(EntityTypeBuilder<AttendanceRecord> b)
    {
        b.HasKey(x => x.Id);
        b.HasIndex(x => new { x.ClassSessionId, x.StudentId }).IsUnique();
        b.HasOne<ClassSession>().WithMany().HasForeignKey(x => x.ClassSessionId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<StudentReadModel>().WithMany().HasForeignKey(x => x.StudentId).OnDelete(DeleteBehavior.Restrict);
        b.Property(x => x.Status).HasConversion<string>().HasMaxLength(16);
        b.Property(x => x.Note).HasMaxLength(1000);
    }
}

public sealed class AttendanceAuditConfiguration : IEntityTypeConfiguration<AttendanceAudit>
{
    public void Configure(EntityTypeBuilder<AttendanceAudit> b)
    {
        b.HasKey(x => x.Id);
        b.HasIndex(x => new { x.ClassSessionId, x.StudentId, x.ModifiedAt });
        b.HasOne<ClassSession>().WithMany().HasForeignKey(x => x.ClassSessionId).OnDelete(DeleteBehavior.Restrict);
        b.Property(x => x.PreviousStatus).HasConversion<string>().HasMaxLength(16);
        b.Property(x => x.NewStatus).HasConversion<string>().HasMaxLength(16);
        b.Property(x => x.Action).HasMaxLength(16);
        b.Property(x => x.Note).HasMaxLength(1000);
    }
}