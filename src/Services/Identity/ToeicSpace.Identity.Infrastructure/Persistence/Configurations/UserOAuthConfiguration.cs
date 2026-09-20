using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ToeicSpace.Identity.Domain.Entities;

namespace ToeicSpace.Identity.Infrastructure.Persistence.Configurations;

public sealed class UserOAuthConfiguration : IEntityTypeConfiguration<UserOAuth>
{
    public void Configure(EntityTypeBuilder<UserOAuth> builder)
    {
        builder.ToTable("UserOAuthAccounts");
        builder.HasKey(account => account.Id);

        builder.Property(account => account.Provider)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(account => account.ProviderSubject)
            .HasMaxLength(255)
            .IsRequired();

        builder.Property(account => account.EmailAtLink)
            .HasMaxLength(254);

        builder.HasIndex(account => new
            {
                account.Provider,
                account.ProviderSubject
            })
            .IsUnique();

        builder.HasOne(account => account.User)
            .WithMany(user => user.OAuthAccounts)
            .HasForeignKey(account => account.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
