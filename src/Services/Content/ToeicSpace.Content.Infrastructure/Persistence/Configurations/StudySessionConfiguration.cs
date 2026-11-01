using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ToeicSpace.Content.Domain.Entities;
using ToeicSpace.Content.Domain.Enums;

namespace ToeicSpace.Content.Infrastructure.Persistence.Configurations;

public sealed class StudySessionConfiguration : IEntityTypeConfiguration<StudySession>
{
    private static readonly JsonSerializerOptions Json = new() { Converters = { new JsonStringEnumConverter() } };

    public void Configure(EntityTypeBuilder<StudySession> b)
    {
        b.ToTable("StudySessions");
        b.HasKey(x => x.Id);
        b.Property(x => x.Mode).HasConversion<string>().HasMaxLength(16).IsRequired();
        b.Property(x => x.SourceKind).HasConversion<string>().HasMaxLength(16).IsRequired();
        b.Property(x => x.Status).HasConversion<string>().HasMaxLength(16).IsRequired();
        b.Property(x => x.SourceTitle).HasMaxLength(200);
        // Set only while Active: the unique index allows one active session per (learner, mode, source).
        b.Property(x => x.ActiveSlot).HasMaxLength(80);
        b.Property(x => x.Version).IsConcurrencyToken();
        b.Ignore(x => x.CurrentWordId);

        foreach (var list in new[] { b.Property(x => x.Queue), b.Property(x => x.Requeued) })
        {
            list.HasConversion(v => JsonSerializer.Serialize(v, Json),
                    v => JsonSerializer.Deserialize<List<Guid>>(v, Json) ?? new List<Guid>())
                .HasColumnType("json").IsRequired();
            list.Metadata.SetValueComparer(new ValueComparer<List<Guid>>(
                (l, r) => l!.SequenceEqual(r!), v => v.Aggregate(0, (h, x) => HashCode.Combine(h, x)), v => v.ToList()));
        }
        b.Property(x => x.Results)
            .HasConversion(v => JsonSerializer.Serialize(v, Json),
                v => JsonSerializer.Deserialize<Dictionary<Guid, ReviewGrade>>(v, Json) ?? new Dictionary<Guid, ReviewGrade>())
            .HasColumnType("json").IsRequired()
            .Metadata.SetValueComparer(new ValueComparer<Dictionary<Guid, ReviewGrade>>(
                (l, r) => l!.Count == r!.Count && !l.Except(r).Any(),
                v => v.Aggregate(0, (h, x) => HashCode.Combine(h, x.Key, x.Value)),
                v => new Dictionary<Guid, ReviewGrade>(v)));

        b.HasIndex(x => new { x.LearnerId, x.ActiveSlot }).IsUnique();
        b.HasIndex(x => new { x.LearnerId, x.Status, x.UpdatedAt });
    }
}

public sealed class LearnerVocabularyPreferencesConfiguration : IEntityTypeConfiguration<LearnerVocabularyPreferences>
{
    public void Configure(EntityTypeBuilder<LearnerVocabularyPreferences> b)
    {
        b.ToTable("LearnerVocabularyPreferences");
        b.HasKey(x => x.LearnerId);
        b.Property(x => x.LearnerId).ValueGeneratedNever();
        b.Property(x => x.DefaultMode).HasConversion<string>().HasMaxLength(16).IsRequired();
        b.Property(x => x.LastView).HasConversion<string>().HasMaxLength(16).IsRequired();
    }
}
