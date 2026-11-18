using ToeicSpace.Identity.Domain.Enums;
using static ToeicSpace.Identity.Application.Features.Analytics.Common.AnalyticsDimensions;
using F = ToeicSpace.Identity.Application.Features.Analytics.Common.AnalyticsFilters;

namespace ToeicSpace.Identity.Application.Features.Analytics.Common;

/// <summary>
/// The whitelist of metrics the Identity analytics endpoint can compute. Every metric is implemented as fixed
/// EF Core LINQ; request values only select entries of this list.
/// </summary>
public static class AnalyticsCatalog
{
    public const string NewRegistrations = "new_registrations";
    public const string TotalAccounts = "total_accounts";
    public const string AccountRoleMix = "account_role_mix";
    public const string AccountStatusMix = "account_status_mix";
    public const string OAuthAccounts = "oauth_accounts";
    public const string Logins = "logins";
    public const string LoginUsers = "login_users";
    public const string InactiveAccounts = "inactive_accounts";
    public const string TeacherCount = "teacher_count";
    public const string StudentCount = "student_count";

    private static readonly string[] TimeAndRoleDimensions = [Day, Week, Month, Role];
    private static readonly string[] RangeAndRoleFilters = [F.From, F.To, F.Role];

    public static readonly IReadOnlyList<AnalyticsMetric> Metrics =
    [
        // Registrations: accounts created in the date range, soft-deleted accounts excluded.
        new(NewRegistrations, "Số tài khoản được tạo trong khoảng thời gian (bỏ tài khoản đã xoá mềm).",
            "accounts", "accounts", AnalyticsSource.Registrations, TimeAndRoleDimensions, RangeAndRoleFilters),

        // Snapshots. Role and status are the current values: their history is not stored.
        new(TotalAccounts, "Số tài khoản tồn tại tại ngày `to`: tạo trước hoặc trong ngày đó và chưa bị xoá mềm tính tới hết ngày đó. Vai trò / trạng thái là giá trị hiện tại.",
            "accounts", "accounts", AnalyticsSource.Accounts, [Role, Status], [F.To, F.Role, F.Status]),
        new(AccountRoleMix, "Phân bố tài khoản hiện có (chưa xoá mềm) theo vai trò.",
            "accounts", "accounts", AnalyticsSource.Accounts, [Role], [F.Status], RequiredDimension: Role),
        new(AccountStatusMix, "Phân bố tài khoản hiện có (chưa xoá mềm) theo trạng thái.",
            "accounts", "accounts", AnalyticsSource.Accounts, [Status], [F.Role], RequiredDimension: Status),
        new(OAuthAccounts, "Số tài khoản hiện có đã liên kết đăng nhập ngoài, theo nhà cung cấp.",
            "accounts", "accounts", AnalyticsSource.OAuthLinks, [Provider], [F.Provider], RequiredDimension: Provider),
        new(TeacherCount, "Số tài khoản giáo viên (vai trò Teacher) hiện có, theo trạng thái.",
            "accounts", "accounts", AnalyticsSource.Accounts, [Status], []),
        new(StudentCount, "Số tài khoản học viên (vai trò User) hiện có, theo trạng thái.",
            "accounts", "accounts", AnalyticsSource.Accounts, [Status], []),

        // Sign-ins, read from refresh tokens: a sign-in starts a session (a chain of tokens sharing one expiry),
        // a renewal rotates the token inside that session. Expired tokens are purged when the account signs in
        // again, so history older than one session lifetime (7 days) is incomplete for returning accounts.
        new(Logins, "Số lượt đăng nhập bắt đầu trong khoảng, xấp xỉ từ refresh token: mỗi phiên mới (chuỗi token cùng hạn) là một lượt, làm mới phiên không tính. Token hết hạn bị dọn khi tài khoản đăng nhập lại nên số liệu cũ hơn ~7 ngày có thể thiếu.",
            "logins", "logins", AnalyticsSource.SignIns, TimeAndRoleDimensions, RangeAndRoleFilters, Suppressed: true),
        new(LoginUsers, "Số tài khoản khác nhau có đăng nhập hoặc làm mới phiên trong khoảng (theo refresh token được cấp).",
            "accounts", "accounts", AnalyticsSource.SignIns, TimeAndRoleDimensions, RangeAndRoleFilters, Suppressed: true),
        new(InactiveAccounts, $"Số tài khoản tồn tại tại ngày `to`, tạo trước đó ít nhất {AnalyticsLimits.InactivityDays} ngày và không được cấp refresh token nào trong {AnalyticsLimits.InactivityDays} ngày tính tới hết ngày `to`.",
            "accounts", "accounts", AnalyticsSource.Activity, [Role], [F.To, F.Role], Personal: true)
    ];

    private static readonly Dictionary<string, AnalyticsMetric> ByName =
        Metrics.ToDictionary(metric => metric.Name, StringComparer.Ordinal);

    public static AnalyticsMetric? Find(string name) => ByName.GetValueOrDefault(name);

    /// <summary>Role a metric is restricted to by definition (it takes no role filter).</summary>
    public static UserRole? FixedRole(string metric) => metric switch
    {
        TeacherCount => UserRole.Teacher,
        StudentCount => UserRole.User,
        _ => null
    };
}
