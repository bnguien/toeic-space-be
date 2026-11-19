using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ToeicSpace.Assessment.Domain.Entities;

namespace ToeicSpace.Assessment.Infrastructure.Persistence.Configurations;

public sealed class ToeicAttemptAnswerConfiguration : IEntityTypeConfiguration<ToeicAttemptAnswer>
{
    public void Configure(EntityTypeBuilder<ToeicAttemptAnswer> builder)
    {
        builder.ToTable("ToeicAttemptAnswers");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.UserAnswer)
            .HasConversion<string>()
            .HasMaxLength(1);

        builder.Property(x => x.CorrectAnswer)
            .HasConversion<string>()
            .HasMaxLength(1)
            .IsRequired();

        builder.Property(x => x.IsFlagged)
            .HasDefaultValue(false);

        builder.HasIndex(x => new { x.AttemptId, x.QuestionId });
        builder.HasIndex(x => x.QuestionId);
        builder.HasIndex(x => new { x.AttemptId, x.SnapshotQuestionId }).IsUnique();
        builder.HasOne<ToeicAttemptQuestion>().WithMany()
            .HasForeignKey(x => new { x.AttemptId, x.SnapshotQuestionId })
            .HasPrincipalKey(x => new { x.AttemptId, x.Id }).OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.Question)
            .WithMany()
            .HasForeignKey(x => x.QuestionId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
