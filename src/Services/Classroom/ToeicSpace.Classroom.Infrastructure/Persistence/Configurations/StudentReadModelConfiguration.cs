using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ToeicSpace.Classroom.Domain.Entities;

namespace ToeicSpace.Classroom.Infrastructure.Persistence.Configurations;

public sealed class StudentReadModelConfiguration : IEntityTypeConfiguration<StudentReadModel>
{
    public void Configure(EntityTypeBuilder<StudentReadModel> b)
    {
        b.ToTable("StudentReadModels");
        b.HasKey(x => x.StudentId);
        b.Property(x => x.Name).HasMaxLength(200).IsRequired();
        b.Property(x => x.Email).HasMaxLength(254).IsRequired();
        b.Property(x => x.Role).HasMaxLength(32).IsRequired();
        b.Property(x => x.Status).HasMaxLength(32).IsRequired();
        b.Property(x => x.Version).IsConcurrencyToken();
        b.HasIndex(x => new { x.IsEligible, x.Name });
    }
}
