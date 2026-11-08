using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ToeicSpace.Content.Domain.Entities;

namespace ToeicSpace.Content.Infrastructure.Persistence.Configurations;

public sealed class VideoCatalogItemConfiguration : IEntityTypeConfiguration<VideoCatalogItem>
{
    public void Configure(EntityTypeBuilder<VideoCatalogItem> b)
    {
        b.ToTable("VideoCatalogItems");
        b.HasKey(x => x.Id);
        b.Property(x => x.VideoId).HasMaxLength(VideoCatalogItem.VideoIdMaxLength).IsRequired();
        b.Property(x => x.TopicSlug).HasMaxLength(VideoCatalogItem.TopicSlugMaxLength).IsRequired();
        b.Property(x => x.TopicTitle).HasMaxLength(VideoCatalogItem.TopicTitleMaxLength).IsRequired();
        b.Property(x => x.Title).HasMaxLength(VideoCatalogItem.TitleMaxLength).IsRequired();
        b.Property(x => x.ChannelHandle).HasMaxLength(VideoCatalogItem.ChannelHandleMaxLength).IsRequired();
        b.Property(x => x.ChannelTitle).HasMaxLength(VideoCatalogItem.ChannelTitleMaxLength).IsRequired();
        b.Property(x => x.Level).HasMaxLength(4).IsRequired();
        b.Property(x => x.Thumbnail).HasMaxLength(VideoCatalogItem.ThumbnailMaxLength).IsRequired();
        b.Property(x => x.Cefr).HasMaxLength(4).IsRequired();
        b.Property(x => x.EmbedReason).HasMaxLength(VideoCatalogItem.EmbedReasonMaxLength).IsRequired();
        b.Property(x => x.SubtitleStatus).HasConversion<string>().HasMaxLength(16).IsRequired();
        b.Property(x => x.EmbedStatus).HasConversion<string>().HasMaxLength(16).IsRequired();
        b.Property(x => x.Track).HasConversion<string>().HasMaxLength(16).IsRequired();
        b.Property(x => x.LevelSource).HasConversion<string>().HasMaxLength(16).IsRequired();
        b.Property(x => x.LevelMetrics).HasColumnType("json");
        b.Ignore(x => x.IsVisible);
        b.HasIndex(x => x.VideoId).IsUnique();
        b.HasIndex(x => new { x.TopicSlug, x.IsActive, x.SubtitleStatus, x.Position });
        b.HasIndex(x => new { x.Track, x.IsActive, x.Difficulty });
    }
}

public sealed class VideoSubtitleConfiguration : IEntityTypeConfiguration<VideoSubtitle>
{
    public void Configure(EntityTypeBuilder<VideoSubtitle> b)
    {
        b.ToTable("VideoSubtitles");
        b.HasKey(x => x.Id);
        b.Property(x => x.VideoId).HasMaxLength(VideoCatalogItem.VideoIdMaxLength).IsRequired();
        b.Property(x => x.Title).HasMaxLength(VideoCatalogItem.TitleMaxLength).IsRequired();
        b.Property(x => x.OriginalLanguage).HasMaxLength(VideoSubtitle.LanguageMaxLength).IsRequired();
        // JSON documents kept as text so imports can compare them byte for byte (MySQL json re-formats).
        b.Property(x => x.Languages).HasColumnType("longtext");
        b.Property(x => x.Tracks).HasColumnType("longtext").IsRequired();
        b.HasIndex(x => x.VideoId).IsUnique();
    }
}

public sealed class VideoWatchConfiguration : IEntityTypeConfiguration<VideoWatch>
{
    public void Configure(EntityTypeBuilder<VideoWatch> b)
    {
        b.ToTable("VideoWatches");
        b.HasKey(x => x.Id);
        b.Property(x => x.VideoId).HasMaxLength(VideoCatalogItem.VideoIdMaxLength).IsRequired();
        b.Property(x => x.Title).HasMaxLength(VideoCatalogItem.TitleMaxLength).IsRequired();
        b.Property(x => x.Language).HasMaxLength(VideoSubtitle.LanguageMaxLength).IsRequired();
        b.Property(x => x.SharerName).HasMaxLength(VideoWatch.SharerNameMaxLength);
        b.Property(x => x.SharerAvatar).HasMaxLength(VideoWatch.SharerAvatarMaxLength);
        b.HasIndex(x => new { x.LearnerId, x.VideoId }).IsUnique();
        b.HasIndex(x => new { x.LearnerId, x.LastOpenedAt });
        b.HasIndex(x => new { x.LearnerId, x.ProgressAt });
        b.HasIndex(x => new { x.IsPublic, x.LastOpenedAt });
    }
}

public sealed class VideoLearnerStateConfiguration : IEntityTypeConfiguration<VideoLearnerState>
{
    public void Configure(EntityTypeBuilder<VideoLearnerState> b)
    {
        b.ToTable("VideoLearnerStates");
        b.HasKey(x => x.LearnerId);
        b.Property(x => x.LearnerId).ValueGeneratedNever();
        b.Property(x => x.ReaderSettings).HasColumnType("json");
        b.Property(x => x.ListenMode).HasMaxLength(VideoLearnerState.ListenModeMaxLength).IsRequired();
        b.Property(x => x.PathTrack).HasConversion<string>().HasMaxLength(16).IsRequired();
    }
}
