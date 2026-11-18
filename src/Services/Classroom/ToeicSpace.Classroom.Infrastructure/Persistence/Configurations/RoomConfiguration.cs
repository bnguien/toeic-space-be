using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ToeicSpace.Classroom.Domain.Entities;

namespace ToeicSpace.Classroom.Infrastructure.Persistence.Configurations;

public sealed class RoomConfiguration : IEntityTypeConfiguration<Room>
{
    public void Configure(EntityTypeBuilder<Room> b)
    {
        b.ToTable("Rooms");
        b.HasKey(x => x.Id);
        b.HasOne(x => x.Center).WithMany().HasForeignKey(x => x.CenterId).OnDelete(DeleteBehavior.Restrict);
        b.Property(x => x.Name).HasMaxLength(200).IsRequired();
        b.HasIndex(x => new { x.CenterId, x.Name }).IsUnique();
        b.Property(x => x.Status).HasConversion<string>().HasMaxLength(16).IsRequired();
        b.HasIndex(x => new { x.CenterId, x.Status });
        b.ToTable(t => t.HasCheckConstraint("CK_Rooms_Capacity", "`Capacity` > 0"));
    }
}
