namespace ToeicSpace.Content.Application.Analytics.Common;

/// <summary>Grouping dimensions accepted in <c>by</c>. Values are fixed names, never column names.</summary>
public static class AnalyticsDimensions
{
    public const string Day = "day";
    public const string Week = "week";
    public const string Month = "month";
    public const string Status = "status";
    public const string Level = "level";
    public const string ExamType = "exam_type";
    public const string Course = "course";
    public const string Kind = "kind";
    public const string Source = "source";
    public const string Stage = "stage";
    public const string Document = "document";
    public const string Video = "video";
    public const string Article = "article";

    public static readonly IReadOnlyDictionary<string, string> Descriptions = new Dictionary<string, string>
    {
        [Day] = "Ngày (UTC) của mốc thời gian metric dùng, dạng yyyy-MM-dd.",
        [Week] = "Tuần ISO (UTC), giá trị là ngày thứ Hai đầu tuần, dạng yyyy-MM-dd.",
        [Month] = "Tháng (UTC), dạng yyyy-MM.",
        [Status] = "Trạng thái nội dung: khoá học (Draft, Published, Archived) hoặc bài học (Draft, Published) tuỳ metric.",
        [Level] = "Trình độ khoá học: Beginner, Intermediate, Advanced.",
        [ExamType] = "Loại kỳ thi của khoá học: ListeningReading, SpeakingWriting, FourSkills.",
        [Course] = "Khoá học: GUID khoá học (lesson_count) hoặc số ID khoá Dictation (dictation_*); xem labels để có mã.",
        [Kind] = "Chế độ phiên học từ vựng: Flashcard, Quiz, Reel.",
        [Source] = "Nguồn từ của phiên học: Set, Due, Folder, Library, Words, Mistakes.",
        [Stage] = "Chặng lộ trình từ vựng (GUID, xem labels để có slug); null = từ tự thêm, không thuộc chặng nào.",
        [Document] = "Tài liệu ngữ pháp: topic-01..topic-44 (chủ đề) hoặc stage-01..stage-05 (bài test chặng).",
        [Video] = "ID video YouTube (xem labels để có tiêu đề nếu video thuộc danh mục).",
        [Article] = "ID bài báo song ngữ (GUID, xem labels để có slug)."
    };
}

/// <summary>Filters accepted by the analytics query endpoint.</summary>
public static class AnalyticsFilters
{
    public const string From = "from";
    public const string To = "to";
    public const string Status = "status";
    public const string Level = "level";
    public const string CourseId = "course_id";

    public static readonly IReadOnlyDictionary<string, string> Descriptions = new Dictionary<string, string>
    {
        [From] = "Ngày bắt đầu (yyyy-MM-dd, UTC, tính cả ngày này). Mặc định: to - 89 ngày.",
        [To] = "Ngày kết thúc (yyyy-MM-dd, UTC, tính cả ngày này). Mặc định: hôm nay. Khoảng tối đa 366 ngày.",
        [Status] = "Trạng thái khoá học (Draft, Published, Archived) hoặc bài học (Draft, Published) tuỳ metric.",
        [Level] = "Trình độ khoá học: 1..3 hoặc Beginner, Intermediate, Advanced.",
        [CourseId] = "Chỉ tính một khoá: GUID khoá học (lesson_count) hoặc số ID khoá Dictation (dictation_*)."
    };
}

public static class AnalyticsVisibility
{
    public const string Admin = "admin";
    public const string Teacher = "teacher";
    public const string Self = "self";
    public const string Public = "public";
}

public static class AnalyticsLimits
{
    /// <summary>Groups with fewer distinct learners are returned with a null value.</summary>
    public const int MinimumLearnersPerGroup = 5;

    public const int MaxDimensions = 2;

    public const int DefaultRowLimit = 100;

    public const int MaxRowLimit = 500;

    public const int MaxRangeDays = 366;

    public const int DefaultRangeDays = 90;

    /// <summary>Upper bound of rows a single metric may load before aggregating in memory.</summary>
    public const int MaxFactRows = 250_000;

    /// <summary>A study session counts for at most this many minutes (sessions left open are not study time).</summary>
    public const int MaxSessionMinutes = 180;

    public static readonly TimeSpan QueryTimeout = TimeSpan.FromSeconds(10);
}
