using Microsoft.EntityFrameworkCore;
using ToeicSpace.Content.Domain.Entities;

namespace ToeicSpace.Content.Infrastructure.Persistence;

public sealed class ContentDbContext(DbContextOptions<ContentDbContext> options) : DbContext(options)
{
    public DbSet<Course> Courses => Set<Course>();
    public DbSet<CoursePromotion> CoursePromotions => Set<CoursePromotion>();
    public DbSet<PromotionReservation> PromotionReservations => Set<PromotionReservation>();
    public DbSet<CourseEnrollment> CourseEnrollments => Set<CourseEnrollment>();
    public DbSet<CourseSlotReservation> CourseSlotReservations => Set<CourseSlotReservation>();
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
    public DbSet<NewsArticle> NewsArticles => Set<NewsArticle>();
    public DbSet<NewsReading> NewsReadings => Set<NewsReading>();
    public DbSet<DictationCourse> DictationCourses => Set<DictationCourse>();
    public DbSet<DictationItem> DictationItems => Set<DictationItem>();
    public DbSet<DictationProgress> DictationProgress => Set<DictationProgress>();
    public DbSet<DictationLearnerState> DictationLearnerStates => Set<DictationLearnerState>();
    public DbSet<VideoCatalogItem> VideoCatalogItems => Set<VideoCatalogItem>();
    public DbSet<VideoSubtitle> VideoSubtitles => Set<VideoSubtitle>();
    public DbSet<VideoWatch> VideoWatches => Set<VideoWatch>();
    public DbSet<VideoLearnerState> VideoLearnerStates => Set<VideoLearnerState>();
    public DbSet<GrammarDocument> GrammarDocuments => Set<GrammarDocument>();
    public DbSet<GrammarLessonProgress> GrammarLessonProgress => Set<GrammarLessonProgress>();
    public DbSet<GrammarAttempt> GrammarAttempts => Set<GrammarAttempt>();
    public DbSet<GrammarQuestionResult> GrammarQuestionResults => Set<GrammarQuestionResult>();
    public DbSet<GrammarLearnerState> GrammarLearnerStates => Set<GrammarLearnerState>();
    public DbSet<GrammarComment> GrammarComments => Set<GrammarComment>();
    public DbSet<GrammarCommentLike> GrammarCommentLikes => Set<GrammarCommentLike>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ContentDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
