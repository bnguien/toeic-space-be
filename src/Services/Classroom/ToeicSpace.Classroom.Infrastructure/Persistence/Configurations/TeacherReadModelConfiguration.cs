using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ToeicSpace.Classroom.Domain.Entities;

namespace ToeicSpace.Classroom.Infrastructure.Persistence.Configurations;

public sealed class TeacherReadModelConfiguration : IEntityTypeConfiguration<TeacherReadModel>
{
    public void Configure(EntityTypeBuilder<TeacherReadModel> b)
    {
        b.ToTable("TeacherReadModels");
        b.HasKey(x => x.TeacherId);
        b.Property(x => x.Name).HasMaxLength(200).IsRequired();
        b.Property(x => x.Email).HasMaxLength(254).IsRequired();
        b.Property(x => x.AvatarUrl).HasMaxLength(2048);
        b.Property(x => x.Role).HasMaxLength(32).IsRequired();
        b.Property(x => x.Status).HasMaxLength(32).IsRequired();
        b.Property(x => x.Version).IsConcurrencyToken();
        b.HasIndex(x => new { x.IsEligible, x.Name });
    }
}
