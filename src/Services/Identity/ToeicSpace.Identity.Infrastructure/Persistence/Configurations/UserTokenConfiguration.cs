using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ToeicSpace.Identity.Domain.Entities;

namespace ToeicSpace.Identity.Infrastructure.Persistence.Configurations;

public sealed class UserTokenConfiguration : IEntityTypeConfiguration<UserToken>
{
    public void Configure(EntityTypeBuilder<UserToken> builder)
    {
        builder.ToTable("UserTokens");
        builder.HasKey(token => token.Id);

        builder.Property(token => token.TokenHash)
            .HasMaxLength(512)
            .IsRequired();

        builder.Property(token => token.Type)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        builder.HasIndex(token => token.UserId);

        builder.HasOne(token => token.User)
            .WithMany(user => user.Tokens)
            .HasForeignKey(token => token.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
