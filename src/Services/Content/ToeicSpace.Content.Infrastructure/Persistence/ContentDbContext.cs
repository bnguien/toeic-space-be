using Microsoft.EntityFrameworkCore;
using ToeicSpace.Content.Domain.Entities;

namespace ToeicSpace.Content.Infrastructure.Persistence;

public sealed class ContentDbContext(DbContextOptions<ContentDbContext> options) : DbContext(options)
{
    public DbSet<Course> Courses => Set<Course>();
    public DbSet<CourseModule> CourseModules => Set<CourseModule>();
    public DbSet<Lesson> Lessons => Set<Lesson>();
    public DbSet<LessonVideo> LessonVideos => Set<LessonVideo>();
    public DbSet<LessonResource> LessonResources => Set<LessonResource>();
    public DbSet<LessonUpload> LessonUploads => Set<LessonUpload>();
    public DbSet<ThumbnailUpload> ThumbnailUploads => Set<ThumbnailUpload>();
    public DbSet<VocabularyStage> VocabularyStages => Set<VocabularyStage>();
    public DbSet<VocabularySet> VocabularySets => Set<VocabularySet>();
    public DbSet<VocabularyWord> VocabularyWords => Set<VocabularyWord>();
    public DbSet<VocabularySetWord> VocabularySetWords => Set<VocabularySetWord>();
    public DbSet<LearnerWordProgress> LearnerWordProgress => Set<LearnerWordProgress>();
    public DbSet<WordReviewLog> WordReviewLogs => Set<WordReviewLog>();
    public DbSet<ReelBackground> ReelBackgrounds => Set<ReelBackground>();
    public DbSet<VocabularyFolder> VocabularyFolders => Set<VocabularyFolder>();
    public DbSet<SavedWord> SavedWords => Set<SavedWord>();
    public DbSet<StudySession> StudySessions => Set<StudySession>();
    public DbSet<LearnerVocabularyPreferences> LearnerVocabularyPreferences => Set<LearnerVocabularyPreferences>();
    public DbSet<WordGlossCacheEntry> WordGlossCache => Set<WordGlossCacheEntry>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ContentDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
