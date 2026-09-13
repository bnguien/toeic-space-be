using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ToeicSpace.Identity.Domain.Entities;

namespace ToeicSpace.Identity.Infrastructure.Persistence.Configurations;

public sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("Users");
        builder.HasKey(user => user.Id);

        builder.Property(user => user.FullName)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(user => user.Email)
            .HasMaxLength(254)
            .IsRequired();

        builder.HasIndex(user => user.Email)
            .IsUnique();

        builder.Property(user => user.Phone)
            .HasMaxLength(16);

        builder.HasIndex(user => user.Phone)
            .IsUnique();

        builder.Property(user => user.PasswordHash)
            .HasMaxLength(512);

        builder.Property(user => user.Role)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(user => user.Status)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();
    }
}
