using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ToeicSpace.Assessment.Domain.Entities;

namespace ToeicSpace.Assessment.Infrastructure.Persistence.Configurations;

public sealed class ToeicQuestionConfiguration : IEntityTypeConfiguration<ToeicQuestion>
{
    /// <summary>
    /// Columns of the FULLTEXT index used by <see cref="Search.MySqlQuestionSearch"/>.
    /// MATCH must list exactly these columns in this order.
    /// </summary>
    internal const string FullTextIndexName = "FT_ToeicQuestions_Content";

    public void Configure(EntityTypeBuilder<ToeicQuestion> builder)
    {
        builder.ToTable("ToeicQuestions");
        builder.HasKey(x => x.Id);
        builder.HasQueryFilter(x => x.DeletedAt == null);

        // Ordinal enums (Part, DifficultyLevel) are stored as numbers so they sort and compare naturally;
        // categorical enums (Section, Status, CorrectAnswer) are stored as readable strings.
        builder.Property(x => x.Part)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(x => x.Section)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(x => x.QuestionText)
            .HasColumnType("text");

        builder.Property(x => x.AudioUrl)
            .HasMaxLength(1000);

        builder.Property(x => x.ImageUrl)
            .HasMaxLength(1000);

        builder.Property(x => x.OptionA)
            .HasColumnType("text")
            .IsRequired();

        builder.Property(x => x.OptionB)
            .HasColumnType("text")
            .IsRequired();

        builder.Property(x => x.OptionC)
            .HasColumnType("text")
            .IsRequired();

        builder.Property(x => x.OptionD)
            .HasColumnType("text");

        builder.Property(x => x.CorrectAnswer)
            .HasConversion<string>()
            .HasMaxLength(1)
            .IsRequired();

        builder.Property(x => x.Explanation)
            .HasColumnType("longtext");

        builder.Property(x => x.Transcript)
            .HasColumnType("longtext");

        builder.Property(x => x.DifficultyLevel)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(x => x.Topic)
            .HasMaxLength(200);

        builder.Property(x => x.Status)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(x => x.Version)
            .IsConcurrencyToken();

        builder.HasIndex(x => new { x.TestId, x.QuestionNumber });
        builder.HasIndex(x => x.PassageId);
        builder.HasIndex(x => x.ExternalId);
        builder.HasIndex(x => new { x.Part, x.DifficultyLevel });
        builder.HasIndex(x => x.Topic);

        builder.HasIndex(x => new { x.QuestionText, x.Explanation, x.Transcript }, FullTextIndexName)
            .IsFullText();
    }
}
