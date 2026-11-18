namespace ToeicSpace.Classroom.Application.Analytics.Common;

/// <summary>Grouping dimensions accepted in <c>by</c>. Values are fixed names, never column names.</summary>
public static class AnalyticsDimensions
{
    public const string Day = "day";
    public const string Week = "week";
    public const string Month = "month";
    public const string Status = "status";
    public const string Class = "class";
    public const string Teacher = "teacher";

    public static readonly IReadOnlyDictionary<string, string> Descriptions = new Dictionary<string, string>
    {
        [Day] = "Ngày (UTC) dạng yyyy-MM-dd: ngày tạo lớp (class_count) hoặc ngày ghi danh (metric ghi danh).",
        [Week] = "Tuần ISO (UTC), giá trị là ngày thứ Hai đầu tuần, dạng yyyy-MM-dd; cùng mốc thời gian với day.",
        [Month] = "Tháng (UTC) dạng yyyy-MM: ngày tạo lớp, ngày ghi danh, hoặc ngày bắt đầu lớp (StartDate) tuỳ metric.",
        [Status] = "Trạng thái: lớp (Draft, Active, Completed, Archived) hoặc ghi danh (Active, Withdrawn) tuỳ metric.",
        [Class] = "ID lớp (xem labels để có mã lớp).",
        [Teacher] = "Mã ẩn danh của giáo viên: 8 ký tự hex đầu của SHA-256, không phải ID và không có nhãn."
    };
}

/// <summary>Filters accepted by the analytics query endpoint.</summary>
public static class AnalyticsFilters
{
    public const string From = "from";
    public const string To = "to";
    public const string Status = "status";
    public const string ClassId = "class_id";

    public static readonly IReadOnlyDictionary<string, string> Descriptions = new Dictionary<string, string>
    {
        [From] = "Ngày bắt đầu (yyyy-MM-dd, UTC, tính cả ngày này). Mặc định: to - 89 ngày.",
        [To] = "Ngày kết thúc (yyyy-MM-dd, UTC, tính cả ngày này). Mặc định: hôm nay. Khoảng tối đa 366 ngày.",
        [Status] = "Trạng thái lớp (Draft, Active, Completed, Archived) hoặc ghi danh (Active, Withdrawn) tuỳ metric.",
        [ClassId] = "Chỉ tính một lớp (GUID)."
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

    public static readonly TimeSpan QueryTimeout = TimeSpan.FromSeconds(10);
}

public static class AnalyticsErrorCodes
{
    public const string Timeout = "analytics_timeout";

    public const string RangeTooLarge = "analytics_range_too_large";
}
