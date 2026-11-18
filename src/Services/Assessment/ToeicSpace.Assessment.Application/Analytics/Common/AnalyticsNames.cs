namespace ToeicSpace.Assessment.Application.Analytics.Common;

/// <summary>Grouping dimensions accepted in <c>by</c>. Values are fixed names, never column names.</summary>
public static class AnalyticsDimensions
{
    public const string Day = "day";
    public const string Week = "week";
    public const string Month = "month";
    public const string Test = "test";
    public const string Mode = "mode";
    public const string Status = "status";
    public const string Part = "part";
    public const string Section = "section";
    public const string Difficulty = "difficulty";
    public const string ScoreBand = "score_band";
    public const string Question = "question";
    public const string PracticeSet = "practice_set";
    public const string Kind = "kind";

    public static readonly IReadOnlyDictionary<string, string> Descriptions = new Dictionary<string, string>
    {
        [Day] = "Ngày (UTC) của lượt làm bài, dạng yyyy-MM-dd.",
        [Week] = "Tuần ISO (UTC), giá trị là ngày thứ Hai đầu tuần, dạng yyyy-MM-dd.",
        [Month] = "Tháng (UTC), dạng yyyy-MM.",
        [Test] = "ID đề thi (xem labels để có mã đề).",
        [Mode] = "Chế độ làm bài: FullTest, Practice, PartPractice.",
        [Status] = "Trạng thái: lượt làm (InProgress, Completed, Abandoned) hoặc nội dung (Draft, Active, Archived) tuỳ metric.",
        [Part] = "Part TOEIC: Part1..Part7.",
        [Section] = "Phần thi: Listening hoặc Reading.",
        [Difficulty] = "Độ khó gắn nhãn: VeryEasy, Easy, Medium, Hard, VeryHard.",
        [ScoreBand] = "Khoảng điểm tổng 100 điểm: 0-99, 100-199, ..., 900-990.",
        [Question] = "ID câu hỏi.",
        [PracticeSet] = "ID bộ luyện tập (xem labels để có mã bộ).",
        [Kind] = "Loại bộ luyện tập: Level, Topic, PartDrill."
    };
}

/// <summary>Filters accepted by the analytics query endpoint.</summary>
public static class AnalyticsFilters
{
    public const string From = "from";
    public const string To = "to";
    public const string TestId = "test_id";
    public const string Part = "part";
    public const string Section = "section";
    public const string Difficulty = "difficulty";
    public const string Status = "status";
    public const string Mode = "mode";

    public static readonly IReadOnlyDictionary<string, string> Descriptions = new Dictionary<string, string>
    {
        [From] = "Ngày bắt đầu (yyyy-MM-dd, UTC, tính cả ngày này). Mặc định: to - 89 ngày.",
        [To] = "Ngày kết thúc (yyyy-MM-dd, UTC, tính cả ngày này). Mặc định: hôm nay. Khoảng tối đa 366 ngày.",
        [TestId] = "Chỉ tính một đề thi (GUID).",
        [Part] = "1..7 hoặc Part1..Part7.",
        [Section] = "Listening hoặc Reading.",
        [Difficulty] = "1..5 hoặc VeryEasy, Easy, Medium, Hard, VeryHard.",
        [Status] = "Trạng thái lượt làm (InProgress, Completed, Abandoned) hoặc nội dung (Draft, Active, Archived) tuỳ metric.",
        [Mode] = "FullTest, Practice hoặc PartPractice."
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

    public const int DefaultEventLimit = 200;

    public const int MaxEventLimit = 1000;

    public const int MaxRangeDays = 366;

    public const int DefaultRangeDays = 90;

    /// <summary>Upper bound of rows a single metric may load before aggregating in memory.</summary>
    public const int MaxFactRows = 250_000;

    public static readonly TimeSpan QueryTimeout = TimeSpan.FromSeconds(10);
}
