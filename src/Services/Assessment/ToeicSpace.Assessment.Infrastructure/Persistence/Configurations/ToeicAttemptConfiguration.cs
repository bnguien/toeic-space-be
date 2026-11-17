using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ToeicSpace.Assessment.Domain.Entities;
using ToeicSpace.Assessment.Domain.Enums;

namespace ToeicSpace.Assessment.Infrastructure.Persistence.Configurations;

public sealed class ToeicAttemptConfiguration : IEntityTypeConfiguration<ToeicAttempt>
{
    public void Configure(EntityTypeBuilder<ToeicAttempt> builder)
    {
        builder.ToTable("ToeicAttempts", t => t.HasCheckConstraint("CK_Attempt_Source",
            "(`TestId` IS NULL) <> (`PracticeSetId` IS NULL)"));
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Status)
            .HasConversion<string>()
            .HasMaxLength(32)
            .HasDefaultValue(AttemptStatus.InProgress)
            .IsRequired();

        builder.Property(x => x.Mode)
            .HasConversion<string>()
            .HasMaxLength(32)
            .HasDefaultValue(AttemptMode.FullTest)
            .IsRequired();

        builder.Property(x => x.TotalQuestions)
            .HasDefaultValue(200);

        builder.HasIndex(x => x.UserId);
        builder.HasIndex(x => x.TestId);
        builder.HasIndex(x => new { x.UserId, x.TestId });
        builder.HasOne<ToeicPracticeSet>().WithMany().HasForeignKey(x => x.PracticeSetId).OnDelete(DeleteBehavior.Restrict);
        builder.Property<Guid?>("ActivePracticeSetId").HasColumnType("char(36)")
            .HasComputedColumnSql("CASE WHEN `Status` = 'InProgress' THEN `PracticeSetId` ELSE NULL END", stored: true);
        builder.HasIndex("UserId", "ActivePracticeSetId").IsUnique();
        builder.HasIndex(x => new { x.Status, x.LastActivityAt, x.PracticeSetId });

        builder.HasMany(x => x.Answers)
            .WithOne(x => x.Attempt)
            .HasForeignKey(x => x.AttemptId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
