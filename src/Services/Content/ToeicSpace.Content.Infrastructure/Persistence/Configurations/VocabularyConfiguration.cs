using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ToeicSpace.Content.Domain.Entities;

namespace ToeicSpace.Content.Infrastructure.Persistence.Configurations;

public sealed class VocabularyStageConfiguration : IEntityTypeConfiguration<VocabularyStage>
{
    public void Configure(EntityTypeBuilder<VocabularyStage> b)
    {
        b.ToTable("VocabularyStages");
        b.HasKey(x => x.Id);
        b.Property(x => x.Slug).HasMaxLength(120).IsRequired();
        b.HasIndex(x => x.Slug).IsUnique();
        b.Property(x => x.Title).HasMaxLength(200).IsRequired();
        b.Property(x => x.Description).HasMaxLength(1000);
        b.HasIndex(x => x.Order);
    }
}

public sealed class VocabularySetConfiguration : IEntityTypeConfiguration<VocabularySet>
{
    public void Configure(EntityTypeBuilder<VocabularySet> b)
    {
        b.ToTable("VocabularySets");
        b.HasKey(x => x.Id);
        b.HasOne(x => x.Stage).WithMany(x => x.Sets).HasForeignKey(x => x.StageId)
            .OnDelete(DeleteBehavior.Restrict);
        b.Property(x => x.Slug).HasMaxLength(120).IsRequired();
        b.Property(x => x.Title).HasMaxLength(200).IsRequired();
        b.Property(x => x.Topic).HasMaxLength(200);
        b.HasIndex(x => new { x.StageId, x.Slug }).IsUnique();
        // Not unique: a deactivated set keeps its old position while a new set may take it.
        b.HasIndex(x => new { x.StageId, x.Order });
    }
}

public sealed class VocabularyWordConfiguration : IEntityTypeConfiguration<VocabularyWord>
{
    public void Configure(EntityTypeBuilder<VocabularyWord> b)
    {
        b.ToTable("VocabularyWords");
        b.HasKey(x => x.Id);
        b.Property(x => x.Headword).HasMaxLength(100).IsRequired();
        b.Property(x => x.NormalizedHeadword).HasMaxLength(100).IsRequired();
        b.Property(x => x.PartOfSpeech).HasConversion<string>().HasMaxLength(24).IsRequired();
        // Official words share OwnerKey = Guid.Empty, so (headword, POS) stays unique among them;
        // custom words are unique per owning learner.
        b.HasIndex(x => new { x.OwnerKey, x.NormalizedHeadword, x.PartOfSpeech }).IsUnique();
        b.Property(x => x.Phonetic).HasMaxLength(100).IsRequired();
        b.Property(x => x.MeaningVi).HasMaxLength(300).IsRequired();
        b.Property(x => x.DefinitionEn).HasMaxLength(1000);
        b.Property(x => x.Example).HasMaxLength(1000);
        b.Property(x => x.ExampleVi).HasMaxLength(1000);
        b.Property(x => x.Cefr).HasMaxLength(8);
        b.Property(x => x.Synonyms)
            .HasConversion(v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                v => JsonSerializer.Deserialize<List<string>>(v, (JsonSerializerOptions?)null) ?? new List<string>())
            .HasColumnType("json").IsRequired()
            .Metadata.SetValueComparer(new ValueComparer<List<string>>(
                (left, right) => left!.SequenceEqual(right!),
                items => items.Aggregate(0, (hash, item) => HashCode.Combine(hash, item)),
                items => items.ToList()));
    }
}

public sealed class VocabularySetWordConfiguration : IEntityTypeConfiguration<VocabularySetWord>
{
    public void Configure(EntityTypeBuilder<VocabularySetWord> b)
    {
        b.ToTable("VocabularySetWords");
        b.HasKey(x => new { x.SetId, x.WordId });
        b.HasOne(x => x.Set).WithMany(x => x.Words).HasForeignKey(x => x.SetId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(x => x.Word).WithMany().HasForeignKey(x => x.WordId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => new { x.SetId, x.Order }).IsUnique();
        b.HasIndex(x => x.WordId);
    }
}

public sealed class LearnerWordProgressConfiguration : IEntityTypeConfiguration<LearnerWordProgress>
{
    public void Configure(EntityTypeBuilder<LearnerWordProgress> b)
    {
        b.ToTable("LearnerWordProgress");
        b.HasKey(x => x.Id);
        b.HasOne(x => x.Word).WithMany().HasForeignKey(x => x.WordId).OnDelete(DeleteBehavior.Restrict);
        b.Property(x => x.LastGrade).HasConversion<string>().HasMaxLength(16);
        b.Property(x => x.Version).IsConcurrencyToken();
        b.HasIndex(x => new { x.LearnerId, x.WordId }).IsUnique();
        b.HasIndex(x => new { x.LearnerId, x.DueOn });
        b.HasIndex(x => new { x.LearnerId, x.MistakePending, x.LastMistakeAt });
    }
}

public sealed class WordReviewLogConfiguration : IEntityTypeConfiguration<WordReviewLog>
{
    public void Configure(EntityTypeBuilder<WordReviewLog> b)
    {
        b.ToTable("WordReviewLogs");
        b.HasKey(x => x.Id);
        b.HasOne<VocabularyWord>().WithMany().HasForeignKey(x => x.WordId).OnDelete(DeleteBehavior.Restrict);
        b.Property(x => x.Mode).HasConversion<string>().HasMaxLength(16).IsRequired();
        b.Property(x => x.Grade).HasConversion<string>().HasMaxLength(16).IsRequired();
        b.HasIndex(x => new { x.LearnerId, x.ClientEventId }).IsUnique();
        b.HasIndex(x => new { x.LearnerId, x.LocalDay });
    }
}

public sealed class ReelBackgroundConfiguration : IEntityTypeConfiguration<ReelBackground>
{
    public void Configure(EntityTypeBuilder<ReelBackground> b)
    {
        b.ToTable("ReelBackgrounds");
        b.HasKey(x => x.Id);
        b.Property(x => x.Provider).HasMaxLength(32).IsRequired();
        b.Property(x => x.ProviderId).HasMaxLength(64).IsRequired();
        b.HasIndex(x => new { x.Provider, x.ProviderId }).IsUnique();
        b.Property(x => x.VideoUrl).HasMaxLength(1000).IsRequired();
        b.Property(x => x.PosterUrl).HasMaxLength(1000).IsRequired();
        b.Property(x => x.PageUrl).HasMaxLength(1000).IsRequired();
        b.Property(x => x.Author).HasMaxLength(200).IsRequired();
        b.Property(x => x.Tags).HasMaxLength(500).IsRequired();
        b.HasIndex(x => x.IsActive);
    }
}

public sealed class VocabularyFolderConfiguration : IEntityTypeConfiguration<VocabularyFolder>
{
    public void Configure(EntityTypeBuilder<VocabularyFolder> b)
    {
        b.ToTable("VocabularyFolders");
        b.HasKey(x => x.Id);
        b.Property(x => x.Name).HasMaxLength(VocabularyFolder.NameMaxLength).IsRequired();
        b.Property(x => x.NormalizedName).HasMaxLength(VocabularyFolder.NameMaxLength).IsRequired();
        b.HasIndex(x => new { x.LearnerId, x.NormalizedName }).IsUnique();
    }
}

public sealed class SavedWordConfiguration : IEntityTypeConfiguration<SavedWord>
{
    public void Configure(EntityTypeBuilder<SavedWord> b)
    {
        b.ToTable("SavedWords");
        b.HasKey(x => x.Id);
        b.Ignore(x => x.IsCustom);
        b.HasOne(x => x.Word).WithMany().HasForeignKey(x => x.WordId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(x => x.Folder).WithMany().HasForeignKey(x => x.FolderId).OnDelete(DeleteBehavior.SetNull);
        b.Property(x => x.Note).HasMaxLength(SavedWord.NoteMaxLength);
        b.Property(x => x.MyPhonetic).HasMaxLength(SavedWord.MyPhoneticMaxLength);
        b.Property(x => x.MyPartOfSpeech).HasConversion<string>().HasMaxLength(24);
        b.Property(x => x.MyMeaningVi).HasMaxLength(SavedWord.MyMeaningMaxLength);
        b.Property(x => x.MyExample).HasMaxLength(SavedWord.MyTextMaxLength);
        b.Property(x => x.MyExampleVi).HasMaxLength(SavedWord.MyTextMaxLength);
        b.Ignore(x => x.HasPersonalEdits);
        b.HasIndex(x => new { x.LearnerId, x.WordId }).IsUnique();
        b.HasIndex(x => new { x.LearnerId, x.FolderId, x.SavedAt });
    }
}
