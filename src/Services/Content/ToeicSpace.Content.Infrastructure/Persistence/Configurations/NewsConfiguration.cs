using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ToeicSpace.Content.Domain.Entities;

namespace ToeicSpace.Content.Infrastructure.Persistence.Configurations;

/// <summary>JSON document columns of the news tables (camelCase keys; compared and snapshotted by value).</summary>
internal static class NewsJson
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    public static PropertyBuilder<T> AsJsonDocument<T>(this PropertyBuilder<T> property, bool required = true)
        where T : class?
    {
        property.HasConversion(
                v => JsonSerializer.Serialize(v, Options),
                v => JsonSerializer.Deserialize<T>(v, Options)!)
            .HasColumnType("json")
            .IsRequired(required)
            .Metadata.SetValueComparer(new ValueComparer<T>(
                (l, r) => JsonSerializer.Serialize(l, Options) == JsonSerializer.Serialize(r, Options),
                v => JsonSerializer.Serialize(v, Options).GetHashCode(),
                v => JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(v, Options), Options)!));
        return property;
    }
}

public sealed class NewsArticleConfiguration : IEntityTypeConfiguration<NewsArticle>
{
    public void Configure(EntityTypeBuilder<NewsArticle> b)
    {
        b.ToTable("NewsArticles");
        b.HasKey(x => x.Id);
        b.Property(x => x.Slug).HasMaxLength(120).IsRequired();
        b.Property(x => x.SourceName).HasMaxLength(80).IsRequired();
        b.Property(x => x.SourceUrl).HasMaxLength(400).IsRequired();
        b.Property(x => x.SourceId).HasMaxLength(40);
        b.Property(x => x.SourceSeries).HasMaxLength(80);
        b.Property(x => x.SourceLevel).HasMaxLength(40);
        b.Property(x => x.Author).HasMaxLength(160);
        b.Property(x => x.License).HasMaxLength(20).IsRequired();
        b.Property(x => x.Attribution).HasMaxLength(300).IsRequired();
        b.Property(x => x.TitleEn).HasMaxLength(240).IsRequired();
        b.Property(x => x.TitleVi).HasMaxLength(240);
        b.Property(x => x.SummaryVi).HasMaxLength(400);
        b.Property(x => x.Paragraphs).AsJsonDocument();
        b.Property(x => x.Vocabulary).AsJsonDocument();
        b.Property(x => x.Stage).HasMaxLength(4).IsRequired();
        b.Property(x => x.Topic).HasMaxLength(20).IsRequired();
        b.Property(x => x.CoverFile).HasMaxLength(80);
        b.Property(x => x.CoverAlt).HasMaxLength(200);
        b.Property(x => x.CoverCredit).HasMaxLength(300);
        b.Property(x => x.CoverLicense).HasMaxLength(20);
        b.Property(x => x.CoverSourceUrl).HasMaxLength(500);
        b.Property(x => x.AudioUrl).HasMaxLength(500);
        b.Property(x => x.Status).HasConversion<string>().HasMaxLength(12).IsRequired();
        b.Ignore(x => x.Sentences);

        b.HasIndex(x => x.Slug).IsUnique();
        b.HasIndex(x => new { x.Status, x.Stage, x.PublishedAt });
        b.HasIndex(x => new { x.Status, x.Topic, x.PublishedAt });
    }
}

public sealed class NewsReadingConfiguration : IEntityTypeConfiguration<NewsReading>
{
    public void Configure(EntityTypeBuilder<NewsReading> b)
    {
        b.ToTable("NewsReadings");
        b.HasKey(x => x.Id);
        b.HasOne(x => x.Article).WithMany().HasForeignKey(x => x.ArticleId).OnDelete(DeleteBehavior.Cascade);
        b.Property(x => x.KnownWords).AsJsonDocument();
        b.Property(x => x.Notes).HasMaxLength(NewsReading.NotesMaxLength);
        b.Property(x => x.Dictation).AsJsonDocument();
        b.Property(x => x.Settings).AsJsonDocument(required: false);
        b.Ignore(x => x.IsDone);

        b.HasIndex(x => new { x.LearnerId, x.ArticleId }).IsUnique();
        b.HasIndex(x => new { x.LearnerId, x.CompletedAt });
        b.HasIndex(x => new { x.LearnerId, x.SettingsUpdatedAt });
    }
}
