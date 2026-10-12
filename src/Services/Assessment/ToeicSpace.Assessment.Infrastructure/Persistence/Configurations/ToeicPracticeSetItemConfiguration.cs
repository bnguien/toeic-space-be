using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ToeicSpace.Assessment.Domain.Entities;

namespace ToeicSpace.Assessment.Infrastructure.Persistence.Configurations;

public sealed class ToeicPracticeSetItemConfiguration : IEntityTypeConfiguration<ToeicPracticeSetItem>
{
    public void Configure(EntityTypeBuilder<ToeicPracticeSetItem> builder)
    {
        builder.ToTable("ToeicPracticeSetItems");
        builder.HasKey(x => new { x.PracticeSetId, x.QuestionId });

        builder.HasIndex(x => new { x.PracticeSetId, x.OrderIndex });
        builder.HasIndex(x => x.QuestionId);

        builder.HasOne(x => x.Question)
            .WithMany(x => x.PracticeSetItems)
            .HasForeignKey(x => x.QuestionId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
