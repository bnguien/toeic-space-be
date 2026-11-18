using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ToeicSpace.Classroom.Domain.Entities;

namespace ToeicSpace.Classroom.Infrastructure.Persistence.Configurations;

public sealed class ClassScheduleConfiguration : IEntityTypeConfiguration<ClassSchedule>
{
    public void Configure(EntityTypeBuilder<ClassSchedule> b)
    {
        b.ToTable("ClassSchedules");
        b.HasKey(x => x.Id);
        b.HasOne<Class>().WithMany().HasForeignKey(x => x.ClassId).OnDelete(DeleteBehavior.Restrict);
        b.Property(x => x.DaysOfWeek).HasConversion(
                x => JsonSerializer.Serialize(x, (JsonSerializerOptions?)null),
                x => JsonSerializer.Deserialize<List<DayOfWeek>>(x, (JsonSerializerOptions?)null) ??
                     new List<DayOfWeek>())
            .HasColumnType("json");
        b.Property(x => x.ExcludeDates).HasConversion(
                x => JsonSerializer.Serialize(x, (JsonSerializerOptions?)null),
                x => JsonSerializer.Deserialize<List<DateOnly>>(x, (JsonSerializerOptions?)null) ??
                     new List<DateOnly>())
            .HasColumnType("json");
        b.Property(x => x.Mode).HasConversion<string>().HasMaxLength(16);
        b.Property(x => x.TimeZoneId).HasMaxLength(100).IsRequired();
        b.Property(x => x.Version).IsConcurrencyToken();
        b.HasIndex(x => new { x.ClassId, x.EffectiveFrom, x.EffectiveTo });
    }
}

public sealed class ClassSessionConfiguration : IEntityTypeConfiguration<ClassSession>
{
    public void Configure(EntityTypeBuilder<ClassSession> b)
    {
        b.ToTable("ClassSessions");
        b.HasKey(x => x.Id);
        b.HasOne<Class>().WithMany().HasForeignKey(x => x.ClassId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<ClassSchedule>().WithMany().HasForeignKey(x => x.ClassScheduleId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Room>().WithMany().HasForeignKey(x => x.RoomId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => new { x.ClassScheduleId, x.OriginalLocalDate }).IsUnique();
        b.HasIndex(x => new { x.TeacherId, x.Status, x.StartAt, x.EndAt });
        b.HasIndex(x => new { x.RoomId, x.Status, x.StartAt, x.EndAt });
        b.HasIndex(x => new { x.ClassId, x.StartAt, x.Id });
        b.Property(x => x.Title).HasMaxLength(200).IsRequired();
        b.Property(x => x.Description).HasMaxLength(2000);
        b.Property(x => x.Notes).HasMaxLength(2000);
        b.Property(x => x.CancellationReason).HasMaxLength(500);
        b.Property(x => x.Mode).HasConversion<string>().HasMaxLength(16);
        b.Property(x => x.SessionType).HasConversion<string>().HasMaxLength(16);
        b.Property(x => x.Status).HasConversion<string>().HasMaxLength(16);
        b.Property(x => x.Version).IsConcurrencyToken();
        b.ToTable(t => t.HasCheckConstraint("CK_ClassSessions_Time", "`StartAt` < `EndAt`"));
    }
}

public sealed class ClassScheduleAuditConfiguration : IEntityTypeConfiguration<ClassScheduleAudit>
{
    public void Configure(EntityTypeBuilder<ClassScheduleAudit> b)
    {
        b.ToTable("ClassScheduleAudits");
        b.HasKey(x => x.Id);
        b.HasOne<ClassSchedule>().WithMany().HasForeignKey(x => x.ClassScheduleId).OnDelete(DeleteBehavior.Restrict);
        b.Property(x => x.Action).HasMaxLength(24).IsRequired();
        b.Property(x => x.PreviousValue).HasColumnType("json");
        b.Property(x => x.NewValue).HasColumnType("json").IsRequired();
        b.HasIndex(x => new { x.ClassScheduleId, x.ChangedAt });
    }
}

public sealed class ClassSessionAuditConfiguration : IEntityTypeConfiguration<ClassSessionAudit>
{
    public void Configure(EntityTypeBuilder<ClassSessionAudit> b)
    {
        b.ToTable("ClassSessionAudits");
        b.HasKey(x => x.Id);
        b.HasOne<ClassSession>().WithMany().HasForeignKey(x => x.ClassSessionId).OnDelete(DeleteBehavior.Restrict);
        b.Property(x => x.Action).HasMaxLength(40).IsRequired();
        b.Property(x => x.Reason).HasMaxLength(500);
        b.Property(x => x.PreviousValue).HasColumnType("json");
        b.Property(x => x.NewValue).HasColumnType("json");
        b.HasIndex(x => new { x.ClassSessionId, x.ChangedAt });
    }
}

public sealed class SchedulingResourceConfiguration : IEntityTypeConfiguration<SchedulingResource>
{
    public void Configure(EntityTypeBuilder<SchedulingResource> b)
    {
        b.ToTable("SchedulingResources");
        b.HasKey(x => x.Key);
        b.Property(x => x.Key).HasMaxLength(40).IsRequired();
    }
}