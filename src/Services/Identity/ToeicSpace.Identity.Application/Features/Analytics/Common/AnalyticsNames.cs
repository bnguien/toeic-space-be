namespace ToeicSpace.Identity.Application.Features.Analytics.Common;

/// <summary>Grouping dimensions accepted in <c>by</c>. Values are fixed names, never column names.</summary>
public static class AnalyticsDimensions
{
    public const string Day = "day";
    public const string Week = "week";
    public const string Month = "month";
    public const string Role = "role";
    public const string Status = "status";
    public const string Provider = "provider";

    public static readonly IReadOnlyDictionary<string, string> Descriptions = new Dictionary<string, string>
    {
        [Day] = "Ngày (UTC) tạo tài khoản hoặc đăng nhập, dạng yyyy-MM-dd.",
        [Week] = "Tuần ISO (UTC), giá trị là ngày thứ Hai đầu tuần, dạng yyyy-MM-dd.",
        [Month] = "Tháng (UTC), dạng yyyy-MM.",
        [Role] = "Vai trò hiện tại của tài khoản: User (học viên), Admin, Teacher.",
        [Status] = "Trạng thái hiện tại của tài khoản: Active, Inactive, Suspended.",
        [Provider] = "Nhà cung cấp đăng nhập ngoài: Google."
    };
}

/// <summary>Filters accepted by the analytics query endpoint.</summary>
public static class AnalyticsFilters
{
    public const string From = "from";
    public const string To = "to";
    public const string Role = "role";
    public const string Status = "status";
    public const string Provider = "provider";

    public static readonly IReadOnlyDictionary<string, string> Descriptions = new Dictionary<string, string>
    {
        [From] = "Ngày bắt đầu (yyyy-MM-dd, UTC, tính cả ngày này). Mặc định: to - 89 ngày.",
        [To] = "Ngày kết thúc / ngày chốt số liệu (yyyy-MM-dd, UTC, tính cả ngày này). Mặc định: hôm nay. Khoảng tối đa 366 ngày.",
        [Role] = "User (hoặc student), Admin, Teacher; chấp nhận số 1..3.",
        [Status] = "Active, Inactive hoặc Suspended; chấp nhận số 1..3.",
        [Provider] = "Nhà cung cấp đăng nhập ngoài: Google (hoặc 1)."
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
    /// <summary>Groups of sign-in metrics with fewer distinct accounts are returned with a null value.</summary>
    public const int MinimumAccountsPerGroup = 5;

    public const int MaxDimensions = 2;

    public const int DefaultRowLimit = 100;

    public const int MaxRowLimit = 500;

    public const int MaxRangeDays = 366;

    public const int DefaultRangeDays = 90;

    /// <summary>An account with no sign-in (and not created) in this many days counts as inactive.</summary>
    public const int InactivityDays = 30;

    /// <summary>Upper bound of rows a single metric may load before aggregating in memory.</summary>
    public const int MaxFactRows = 250_000;

    public static readonly TimeSpan QueryTimeout = TimeSpan.FromSeconds(10);
}
