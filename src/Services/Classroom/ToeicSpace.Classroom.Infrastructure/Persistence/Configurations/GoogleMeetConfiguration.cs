using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ToeicSpace.Classroom.Domain.Entities;

namespace ToeicSpace.Classroom.Infrastructure.Persistence.Configurations;

public sealed class TeacherGoogleConnectionConfiguration : IEntityTypeConfiguration<TeacherGoogleConnection>
{
    public void Configure(EntityTypeBuilder<TeacherGoogleConnection> b)
    {
        b.ToTable("TeacherGoogleConnections");
        b.HasKey(x => x.TeacherId);
        b.HasOne<TeacherReadModel>().WithMany().HasForeignKey(x => x.TeacherId).OnDelete(DeleteBehavior.Restrict);
        b.Property(x => x.Email).HasMaxLength(320).IsRequired();
        b.Property(x => x.EncryptedRefreshToken).HasColumnType("text").IsRequired();
        b.Property(x => x.Version).IsConcurrencyToken();
    }
}

public sealed class GoogleOAuthAttemptConfiguration : IEntityTypeConfiguration<GoogleOAuthAttempt>
{
    public void Configure(EntityTypeBuilder<GoogleOAuthAttempt> b)
    {
        b.ToTable("GoogleOAuthAttempts");
        b.HasKey(x => x.StateHash);
        b.Property(x => x.StateHash).HasMaxLength(64);
        b.HasIndex(x => x.ExpiresAt);
    }
}

public sealed class SessionMeetProvisioningConfiguration : IEntityTypeConfiguration<SessionMeetProvisioning>
{
    public void Configure(EntityTypeBuilder<SessionMeetProvisioning> b)
    {
        b.ToTable("SessionMeetProvisionings");
        b.HasKey(x => x.ClassSessionId);
        b.HasOne<ClassSession>().WithMany().HasForeignKey(x => x.ClassSessionId).OnDelete(DeleteBehavior.Restrict);
        b.Property(x => x.Status).HasMaxLength(32).IsRequired();
        b.Property(x => x.SpaceName).HasMaxLength(128);
        b.Property(x => x.MeetingUrl).HasMaxLength(512);
        b.Property(x => x.ErrorCode).HasMaxLength(64);
        b.Property(x => x.Version).IsConcurrencyToken();
        b.HasIndex(x => new { x.Status, x.NextAttemptAt });
    }
}