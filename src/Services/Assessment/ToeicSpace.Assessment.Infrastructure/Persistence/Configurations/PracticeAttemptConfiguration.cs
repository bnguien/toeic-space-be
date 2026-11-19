using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ToeicSpace.Assessment.Domain.Entities;

namespace ToeicSpace.Assessment.Infrastructure.Persistence.Configurations;

public sealed class PracticeAttemptStateConfiguration : IEntityTypeConfiguration<PracticeAttemptState>
{
    public void Configure(EntityTypeBuilder<PracticeAttemptState> b)
    {
        b.ToTable("PracticeAttemptStates");
        b.HasKey(x => new { x.UserId, x.PracticeSetId });
    }
}

public sealed class AttemptSnapshotConfiguration : IEntityTypeConfiguration<ToeicAttemptSnapshot>
{
    public void Configure(EntityTypeBuilder<ToeicAttemptSnapshot> b)
    {
        b.ToTable("ToeicAttemptSnapshots");
        b.HasKey(x => x.AttemptId);
        b.Property(x => x.Payload).HasColumnType("longtext");
        b.HasOne(x => x.Attempt).WithOne(x => x.Snapshot).HasForeignKey<ToeicAttemptSnapshot>(x => x.AttemptId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class AttemptQuestionConfiguration : IEntityTypeConfiguration<ToeicAttemptQuestion>
{
    public void Configure(EntityTypeBuilder<ToeicAttemptQuestion> b)
    {
        b.ToTable("ToeicAttemptQuestions");
        b.HasKey(x => x.Id);
        b.HasAlternateKey(x => new { x.AttemptId, x.Id });
        b.HasIndex(x => new { x.AttemptId, x.SourceQuestionId }).IsUnique();
        b.Property(x => x.Payload).HasColumnType("longtext");
        b.HasOne<ToeicAttempt>().WithMany().HasForeignKey(x => x.AttemptId).OnDelete(DeleteBehavior.Cascade);
    }
}
