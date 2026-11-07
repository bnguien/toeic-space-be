using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ToeicSpace.Content.Domain.Entities;

namespace ToeicSpace.Content.Infrastructure.Persistence.Configurations;

/// <summary>JSON columns of the dictation tables: camelCase English keys, enums as strings.</summary>
internal static class DictationJson
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    public static PropertyBuilder<T> HasJsonConversion<T>(this PropertyBuilder<T> property) where T : class, new()
    {
        property.HasConversion(
                value => JsonSerializer.Serialize(value, Options),
                json => JsonSerializer.Deserialize<T>(json, Options) ?? new T())
            .HasColumnType("json")
            .Metadata.SetValueComparer(new ValueComparer<T>(
                (left, right) => JsonSerializer.Serialize(left, Options) == JsonSerializer.Serialize(right, Options),
                value => JsonSerializer.Serialize(value, Options).GetHashCode(),
                value => JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(value, Options), Options)!));
        return property;
    }
}

public sealed class DictationCourseConfiguration : IEntityTypeConfiguration<DictationCourse>
{
    public void Configure(EntityTypeBuilder<DictationCourse> b)
    {
        b.ToTable("DictationCourses");
        b.HasKey(x => x.Id);
        b.Property(x => x.Code).HasMaxLength(40).IsRequired();
        b.HasIndex(x => x.Code).IsUnique();
        b.Property(x => x.Pack).HasMaxLength(40).IsRequired();
        b.Property(x => x.Title).HasMaxLength(200).IsRequired();
        // Pack listing and roadmap: active courses of a pack in order.
        b.HasIndex(x => new { x.Pack, x.IsActive, x.Order });
    }
}

public sealed class DictationItemConfiguration : IEntityTypeConfiguration<DictationItem>
{
    public void Configure(EntityTypeBuilder<DictationItem> b)
    {
        b.ToTable("DictationItems");
        b.HasKey(x => x.Id);
        b.HasOne(x => x.Course).WithMany(x => x.Items).HasForeignKey(x => x.CourseId).OnDelete(DeleteBehavior.Restrict);
        b.Property(x => x.SourceCode).HasMaxLength(40).IsRequired();
        b.HasIndex(x => x.SourceCode).IsUnique();
        b.Property(x => x.Topic).HasMaxLength(40).IsRequired();
        b.Property(x => x.Text).HasMaxLength(1000).IsRequired();
        b.Property(x => x.Translation).HasMaxLength(1000).IsRequired();
        b.Property(x => x.Ipa).HasMaxLength(2000).IsRequired();
        b.Property(x => x.VoiceKey).HasMaxLength(30).IsRequired();
        b.Property(x => x.Keywords).HasJsonConversion().IsRequired();
        b.Property(x => x.Notes).HasJsonConversion().IsRequired();
        b.Property(x => x.Vocab).HasJsonConversion().IsRequired();
        // Sentences of a course in order (play page, roadmap ids, vocab).
        b.HasIndex(x => new { x.CourseId, x.IsActive, x.Order });
    }
}

public sealed class DictationProgressConfiguration : IEntityTypeConfiguration<DictationProgress>
{
    public void Configure(EntityTypeBuilder<DictationProgress> b)
    {
        b.ToTable("DictationProgress");
        b.HasKey(x => x.Id);
        b.HasOne(x => x.Item).WithMany().HasForeignKey(x => x.ItemId).OnDelete(DeleteBehavior.Cascade);
        b.HasIndex(x => new { x.LearnerId, x.ItemId }).IsUnique();
        b.Property(x => x.Version).IsConcurrencyToken();
    }
}

public sealed class DictationLearnerStateConfiguration : IEntityTypeConfiguration<DictationLearnerState>
{
    public void Configure(EntityTypeBuilder<DictationLearnerState> b)
    {
        b.ToTable("DictationLearnerStates");
        b.HasKey(x => x.LearnerId);
        b.Property(x => x.LearnerId).ValueGeneratedNever();
        b.Property(x => x.Stars).HasJsonConversion().IsRequired();
        b.Property(x => x.DailySeconds).HasJsonConversion().IsRequired();
        b.Property(x => x.BasicSettings).HasColumnType("json");
        b.Property(x => x.ProSettings).HasColumnType("json");
        b.Property(x => x.Voice).HasMaxLength(30).IsRequired();
        b.Property(x => x.Sound)
            .HasConversion(
                value => value == null ? null : JsonSerializer.Serialize(value, DictationJson.Options),
                json => json == null ? null : JsonSerializer.Deserialize<DictationSoundPreferences>(json, DictationJson.Options))
            .HasColumnType("json");
        b.Property(x => x.Version).IsConcurrencyToken();
    }
}
