using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ToeicSpace.Classroom.Domain.Entities;

namespace ToeicSpace.Classroom.Infrastructure.Persistence.Configurations;

public sealed class ClassRoomChangeConfiguration : IEntityTypeConfiguration<ClassRoomChange>
{
    public void Configure(EntityTypeBuilder<ClassRoomChange> b)
    {
        b.ToTable("ClassRoomChanges");
        b.HasKey(x => x.Id);
        b.HasOne<Class>().WithMany().HasForeignKey(x => x.ClassId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Room>().WithMany().HasForeignKey(x => x.PreviousRoomId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Room>().WithMany().HasForeignKey(x => x.NewRoomId).OnDelete(DeleteBehavior.Restrict);
        b.Property(x => x.Reason).HasMaxLength(500);
        b.HasIndex(x => new { x.ClassId, x.ChangedAt });
    }
}