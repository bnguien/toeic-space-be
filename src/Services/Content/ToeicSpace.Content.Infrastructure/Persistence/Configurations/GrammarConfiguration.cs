using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ToeicSpace.Content.Domain.Entities;

namespace ToeicSpace.Content.Infrastructure.Persistence.Configurations;

internal static class GrammarJson
{
    private static readonly JsonSerializerOptions Options = new();

    public static PropertyBuilder<T> Json<T>(this PropertyBuilder<T> property, Func<T, T> copy, Func<T, T, bool> equals)
        where T : class, new()
    {
        property.HasConversion(v => JsonSerializer.Serialize(v, Options), v => JsonSerializer.Deserialize<T>(v, Options) ?? new T())
            .HasColumnType("json").IsRequired();
        property.Metadata.SetValueComparer(new ValueComparer<T>((l, r) => equals(l!, r!),
            v => JsonSerializer.Serialize(v, Options).GetHashCode(), v => copy(v)));
        return property;
    }
}

public sealed class GrammarDocumentConfiguration : IEntityTypeConfiguration<GrammarDocument>
{
    public void Configure(EntityTypeBuilder<GrammarDocument> b)
    {
        b.ToTable("GrammarDocuments");
        b.HasKey(x => x.Id);
        b.Property(x => x.Kind).HasConversion<string>().HasMaxLength(16).IsRequired();
        b.Property(x => x.Key).HasMaxLength(8).IsRequired();
        // longtext, not json: the stored document stays byte-identical to its ContentHash (MySQL json re-orders keys).
        b.Property(x => x.Content).HasColumnType("longtext").IsRequired();
        b.Property(x => x.ContentHash).HasMaxLength(64).IsRequired();
        b.HasIndex(x => new { x.Kind, x.Key }).IsUnique();
    }
}

public sealed class GrammarLessonProgressConfiguration : IEntityTypeConfiguration<GrammarLessonProgress>
{
    public void Configure(EntityTypeBuilder<GrammarLessonProgress> b)
    {
        b.ToTable("GrammarLessonProgress");
        b.HasKey(x => x.Id);
        b.Property(x => x.Topic).HasMaxLength(2).IsRequired();
        b.Property(x => x.QuickCheckBest).Json(v => new Dictionary<string, int>(v),
            (l, r) => l.Count == r.Count && !l.Except(r).Any());
        b.Property(x => x.Version).IsConcurrencyToken();
        b.HasIndex(x => new { x.LearnerId, x.Topic }).IsUnique();
    }
}

public sealed class GrammarAttemptConfiguration : IEntityTypeConfiguration<GrammarAttempt>
{
    public void Configure(EntityTypeBuilder<GrammarAttempt> b)
    {
        b.ToTable("GrammarAttempts");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).ValueGeneratedOnAdd();
        b.Property(x => x.Topic).HasMaxLength(2);
        b.Property(x => x.Stage).HasMaxLength(2);
        b.Property(x => x.Block).HasMaxLength(8).IsRequired();
        b.Property(x => x.Answers).HasColumnType("json").IsRequired();
        b.Property(x => x.Results).Json(v => new Dictionary<string, bool>(v),
            (l, r) => l.Count == r.Count && !l.Except(r).Any());
        b.Property(x => x.FixedQuestionIds).Json(v => v.ToList(), (l, r) => l.SequenceEqual(r));
        // Stage-test status (best, passed) is computed from the learner's stage attempts.
        b.HasIndex(x => new { x.LearnerId, x.Stage });
    }
}

public sealed class GrammarQuestionResultConfiguration : IEntityTypeConfiguration<GrammarQuestionResult>
{
    public void Configure(EntityTypeBuilder<GrammarQuestionResult> b)
    {
        b.ToTable("GrammarQuestionResults");
        b.HasKey(x => x.Id);
        b.Property(x => x.QuestionId).HasMaxLength(40).IsRequired();
        b.Property(x => x.Topic).HasMaxLength(2).IsRequired();
        b.Property(x => x.Block).HasMaxLength(8).IsRequired();
        b.Property(x => x.UnitCode).HasMaxLength(20).IsRequired();
        b.Property(x => x.TrapCode).HasMaxLength(24).IsRequired();
        b.HasIndex(x => new { x.LearnerId, x.QuestionId }).IsUnique();
        b.HasIndex(x => new { x.LearnerId, x.LastCorrect, x.WrongCount }); // mistake book + review count
        b.HasIndex(x => new { x.LearnerId, x.TrapCode, x.WrongCount }); // frequent traps
    }
}

public sealed class GrammarLearnerStateConfiguration : IEntityTypeConfiguration<GrammarLearnerState>
{
    public void Configure(EntityTypeBuilder<GrammarLearnerState> b)
    {
        b.ToTable("GrammarLearnerStates");
        b.HasKey(x => x.LearnerId);
        b.Property(x => x.LearnerId).ValueGeneratedNever();
        b.Property(x => x.PositionTopic).HasMaxLength(2);
        b.Property(x => x.PositionPart).HasMaxLength(12);
        b.Property(x => x.PositionAnchor).HasMaxLength(120).IsRequired();
        b.Property(x => x.KnownWords).Json(
            v => v.ToDictionary(x => x.Key, x => x.Value.ToList()),
            (l, r) => l.Count == r.Count && l.All(x => r.TryGetValue(x.Key, out var o) && o.SequenceEqual(x.Value)));
        b.Property(x => x.Version).IsConcurrencyToken();
    }
}

public sealed class GrammarCommentConfiguration : IEntityTypeConfiguration<GrammarComment>
{
    public void Configure(EntityTypeBuilder<GrammarComment> b)
    {
        b.ToTable("GrammarComments");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).ValueGeneratedOnAdd();
        b.Property(x => x.Target).HasMaxLength(24).IsRequired();
        b.Property(x => x.AuthorName).HasMaxLength(80).IsRequired();
        b.Property(x => x.Content).HasColumnType("text").IsRequired();
        b.HasIndex(x => new { x.Target, x.ParentId, x.Id }); // roots newest first, replies of a root
        b.HasIndex(x => new { x.LearnerId, x.Id }); // cooldown / duplicate check
    }
}

public sealed class GrammarCommentLikeConfiguration : IEntityTypeConfiguration<GrammarCommentLike>
{
    public void Configure(EntityTypeBuilder<GrammarCommentLike> b)
    {
        b.ToTable("GrammarCommentLikes");
        b.HasKey(x => new { x.CommentId, x.LearnerId });
        b.HasOne<GrammarComment>().WithMany().HasForeignKey(x => x.CommentId).OnDelete(DeleteBehavior.Cascade);
    }
}
