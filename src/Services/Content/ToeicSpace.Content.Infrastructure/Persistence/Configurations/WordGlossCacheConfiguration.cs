using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ToeicSpace.Content.Domain.Entities;

namespace ToeicSpace.Content.Infrastructure.Persistence.Configurations;

public sealed class WordGlossCacheConfiguration : IEntityTypeConfiguration<WordGlossCacheEntry>
{
    public const int NormalizedTextMaxLength = 100;
    public const int MeaningMaxLength = 300;
    public const int TextMaxLength = 400;
    public const int NoteMaxLength = 500;

    public void Configure(EntityTypeBuilder<WordGlossCacheEntry> b)
    {
        b.ToTable("WordGlossCache");
        b.HasKey(x => x.Id);
        b.Property(x => x.NormalizedText).HasMaxLength(NormalizedTextMaxLength).IsRequired();
        b.Property(x => x.Kind).HasMaxLength(16).IsRequired();
        b.HasIndex(x => new { x.NormalizedText, x.Kind }).IsUnique();
        b.Property(x => x.Headword).HasMaxLength(NormalizedTextMaxLength).IsRequired();
        b.Property(x => x.Phonetic).HasMaxLength(100);
        b.Property(x => x.PartOfSpeech).HasConversion<string>().HasMaxLength(24);
        b.Property(x => x.MeaningVi).HasMaxLength(MeaningMaxLength).IsRequired();
        b.Property(x => x.DefinitionEn).HasMaxLength(TextMaxLength);
        b.Property(x => x.Example).HasMaxLength(TextMaxLength);
        b.Property(x => x.ExampleVi).HasMaxLength(TextMaxLength);
        b.Property(x => x.Cefr).HasMaxLength(8);
        b.Property(x => x.PhraseType).HasMaxLength(32);
        b.Property(x => x.Usage).HasMaxLength(NoteMaxLength);
        b.Property(x => x.Explanation).HasMaxLength(NoteMaxLength);
        b.Property(x => x.LiteralMeaning).HasMaxLength(NoteMaxLength);
        b.Property(x => x.Sources)
            .HasConversion(v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                v => JsonSerializer.Deserialize<Dictionary<string, string>>(v, (JsonSerializerOptions?)null)
                     ?? new Dictionary<string, string>())
            .HasColumnType("json").IsRequired()
            .Metadata.SetValueComparer(new ValueComparer<Dictionary<string, string>>(
                (l, r) => l!.Count == r!.Count && !l.Except(r).Any(),
                v => v.Aggregate(0, (h, x) => HashCode.Combine(h, x.Key, x.Value)),
                v => new Dictionary<string, string>(v)));
        b.HasIndex(x => x.ExpiresAt);
    }
}
