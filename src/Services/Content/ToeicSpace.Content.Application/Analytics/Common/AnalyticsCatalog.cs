using static ToeicSpace.Content.Application.Analytics.Common.AnalyticsDimensions;
using F = ToeicSpace.Content.Application.Analytics.Common.AnalyticsFilters;
using V = ToeicSpace.Content.Application.Analytics.Common.AnalyticsVisibility;

namespace ToeicSpace.Content.Application.Analytics.Common;

/// <summary>
/// The whitelist of metrics the Content analytics endpoint can compute. Every metric is implemented as fixed
/// EF Core LINQ in the analytics repository; request values only select entries of this list.
/// </summary>
public static class AnalyticsCatalog
{
    public const string CourseCount = "course_count";
    public const string LessonCount = "lesson_count";
    public const string StudySessions = "study_sessions";
    public const string StudyTime = "study_time";
    public const string ActiveLearners = "active_learners";
    public const string VocabularyReviews = "vocabulary_reviews";
    public const string VocabularyReviewAccuracy = "vocabulary_review_accuracy";
    public const string VocabularyReviewRetention = "vocabulary_review_retention";
    public const string VocabularyWordsLearned = "vocabulary_words_learned";
    public const string SavedWords = "saved_words";
    public const string DictationAttempts = "dictation_attempts";
    public const string DictationCompletion = "dictation_completion";
    public const string GrammarAttempts = "grammar_attempts";
    public const string GrammarAccuracy = "grammar_accuracy";
    public const string GrammarLessonCompletion = "grammar_lesson_completion";
    public const string VideoWatches = "video_watches";
    public const string VideoWatchTime = "video_watch_time";
    public const string NewsReads = "news_reads";
    public const string NewsReadCompletion = "news_read_completion";

    private static readonly string[] TimeDimensions = [Day, Week, Month];
    private static readonly string[] SessionDimensions = [Day, Week, Month, Kind, Source];
    private static readonly string[] VocabularyDimensions = [Day, Week, Month, Stage];
    private static readonly string[] GrammarDimensions = [Day, Week, Month, Document];
    private static readonly string[] VideoDimensions = [Day, Week, Month, Video];
    private static readonly string[] NewsDimensions = [Day, Week, Month, Article];

    private static readonly string[] RangeFilters = [F.From, F.To];
    private static readonly string[] DictationFilters = [F.From, F.To, F.CourseId];

    public static readonly IReadOnlyList<AnalyticsMetric> Metrics =
    [
        // Course catalogue: current content, no learner data, so nothing is suppressed.
        new(CourseCount, "Số khoá học hiện có. Nhóm theo day / week / month dùng ngày tạo khoá (CreatedAt).",
            "courses", "courses", AnalyticsSource.Courses, [Day, Week, Month, Status, Level, ExamType], [F.Status, F.Level],
            Suppressed: false),
        new(LessonCount, "Số bài học hiện có của từng khoá học (qua chương / module).",
            "lessons", "lessons", AnalyticsSource.Lessons, [Course, Status], [F.CourseId, F.Status],
            Suppressed: false, RequiredDimension: Course),

        // Vocabulary study sessions (Flashcard / Quiz / Reel), by start time.
        new(StudySessions, "Số phiên học từ vựng bắt đầu trong khoảng thời gian (theo StartedAt).",
            "sessions", "sessions", AnalyticsSource.StudySessions, SessionDimensions, RangeFilters),
        new(StudyTime, "Tổng số phút học của các phiên học từ vựng bắt đầu trong khoảng: từ lúc bắt đầu tới lần trả lời / hoàn thành cuối, mỗi phiên tối đa 180 phút.",
            "minutes", "learners", AnalyticsSource.StudySessions, SessionDimensions, RangeFilters),
        new(ActiveLearners, "Số học viên khác nhau có hoạt động học trong khoảng: phiên học, ôn từ, lưu từ, Dictation, ngữ pháp, video hoặc đọc báo.",
            "learners", "learners", AnalyticsSource.ActiveLearners, TimeDimensions, RangeFilters),

        // Vocabulary reviews (WordReviewLog), by review time.
        new(VocabularyReviews, "Số lượt ôn từ vựng được ghi nhận (mọi điểm, kể cả Seen của Reel).",
            "reviews", "reviews", AnalyticsSource.Reviews, VocabularyDimensions, RangeFilters),
        new(VocabularyReviewAccuracy, "Tỉ lệ trả lời Good/Easy trên các lượt ôn có chấm điểm (bỏ Seen).",
            "ratio", "reviews", AnalyticsSource.Reviews, VocabularyDimensions, RangeFilters),
        new(VocabularyReviewRetention, "Tỉ lệ nhớ: lượt ôn từ đã có lịch (interval trước > 0) không bị chấm Forget, trên tổng lượt ôn như vậy (bỏ Seen).",
            "ratio", "reviews", AnalyticsSource.Reviews, VocabularyDimensions, RangeFilters),
        new(VocabularyWordsLearned, "Số cặp học viên × từ đạt mức thuộc (interval từ 21 ngày trở lên) lần ôn trong khoảng.",
            "words", "learners", AnalyticsSource.MasteredWords, VocabularyDimensions, RangeFilters),
        new(SavedWords, "Số từ được lưu vào sổ từ trong khoảng (theo SavedAt).",
            "words", "words", AnalyticsSource.SavedWords, TimeDimensions, RangeFilters),

        // Dictation: one progress row per learner and sentence; only the latest attempt time is stored.
        new(DictationAttempts, "Số câu Dictation (học viên × câu) có lần nộp gần nhất rơi vào khoảng (DB chỉ lưu lần nộp cuối của mỗi câu).",
            "items", "items", AnalyticsSource.Dictation, [Day, Week, Month, Course], DictationFilters),
        new(DictationCompletion, "Tỉ lệ câu Dictation đã đạt (Passed) trên các câu đã làm, theo lần nộp gần nhất trong khoảng.",
            "ratio", "items", AnalyticsSource.Dictation, [Month, Course], DictationFilters),

        // Grammar.
        new(GrammarAttempts, "Số lượt nộp bài ngữ pháp (quick check, mini test, test chặng) trong khoảng (theo CreatedAt).",
            "attempts", "attempts", AnalyticsSource.GrammarAttempts, GrammarDimensions, RangeFilters),
        new(GrammarAccuracy, "Tỉ lệ đúng ngay lần đầu trên các câu ngữ pháp đã trả lời; mốc thời gian là lần trả lời gần nhất (AnsweredAt), nhóm theo chủ đề chính của câu.",
            "ratio", "answers", AnalyticsSource.GrammarAnswers, GrammarDimensions, RangeFilters),
        new(GrammarLessonCompletion, "Tỉ lệ chủ đề ngữ pháp đã hoàn thành trên số chủ đề đã bắt đầu, theo lần cập nhật tiến độ gần nhất (UpdatedAt) trong khoảng.",
            "ratio", "lessons", AnalyticsSource.GrammarLessons, [Month, Document], RangeFilters),

        // Bilingual video.
        new(VideoWatches, "Số lượt mở video lần đầu (học viên × video) trong khoảng (theo CreatedAt), kể cả video đã xoá khỏi danh sách cá nhân.",
            "watches", "watches", AnalyticsSource.VideoWatches, VideoDimensions, RangeFilters),
        new(VideoWatchTime, "Tổng số giây đã xem (xa nhất % × thời lượng video) của các video có tiến độ cập nhật lần cuối trong khoảng (theo ProgressAt).",
            "seconds", "learners", AnalyticsSource.VideoWatches, VideoDimensions, RangeFilters),

        // Bilingual news.
        new(NewsReads, "Số lượt mở bài báo song ngữ (học viên × bài) trong khoảng (theo OpenedAt).",
            "reads", "reads", AnalyticsSource.NewsReadings, NewsDimensions, RangeFilters),
        new(NewsReadCompletion, "Tỉ lệ bài báo đã đọc xong trên số bài đã mở trong khoảng (theo OpenedAt).",
            "ratio", "reads", AnalyticsSource.NewsReadings, [Month, Article], RangeFilters)
    ];

    /// <summary>Metrics this service will serve later; listed so callers know what is not available yet.</summary>
    public static readonly IReadOnlyList<PlannedAnalyticsMetric> Planned =
    [
        new("course_lesson_progress", "content",
            "Tiến độ học viên theo khoá học / bài học: chưa có dữ liệu tiến độ bài học khoá học trong Content (chỉ có lesson_count).",
            V.Teacher)
    ];

    private static readonly Dictionary<string, AnalyticsMetric> ByName =
        Metrics.ToDictionary(metric => metric.Name, StringComparer.Ordinal);

    public static AnalyticsMetric? Find(string name) => ByName.GetValueOrDefault(name);

    public static bool IsPlanned(string name) => Planned.Any(metric => metric.Name == name);
}
