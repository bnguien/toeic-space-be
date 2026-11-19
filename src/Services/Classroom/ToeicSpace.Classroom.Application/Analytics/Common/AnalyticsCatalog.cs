using static ToeicSpace.Classroom.Application.Analytics.Common.AnalyticsDimensions;
using F = ToeicSpace.Classroom.Application.Analytics.Common.AnalyticsFilters;
using V = ToeicSpace.Classroom.Application.Analytics.Common.AnalyticsVisibility;

namespace ToeicSpace.Classroom.Application.Analytics.Common;

/// <summary>
/// The whitelist of metrics the Classroom analytics endpoint can compute. Every metric reads fixed EF Core
/// queries; request values only select entries of this list. Soft-deleted classes (status Deleted) never count.
/// </summary>
public static class AnalyticsCatalog
{
    public const string ClassCount = "class_count";
    public const string ActiveClasses = "active_classes";
    public const string ClassEnrollment = "class_enrollment";
    public const string EnrollmentStatusMix = "enrollment_status_mix";
    public const string WithdrawRate = "withdraw_rate";
    public const string ClassSizeAvg = "class_size_avg";
    public const string EnrolledLearners = "enrolled_learners";
    public const string TeacherLoad = "teacher_load";

    public static readonly IReadOnlyList<AnalyticsMetric> Metrics =
    [
        // Classes: one row per class, soft-deleted classes excluded.
        new(ClassCount, "Số lớp được tạo trong khoảng thời gian (theo ngày tạo lớp CreatedAt).",
            "classes", "classes", AnalyticsSource.Classes, [Day, Week, Month, Status], [F.From, F.To, F.Status]),
        new(ActiveClasses, "Số lớp đang mở hiện tại (Draft sắp mở + Active đang học), không theo khoảng ngày; month = tháng bắt đầu lớp (StartDate).",
            "classes", "classes", AnalyticsSource.Classes, [Status, Month], []),

        // Enrollments: one row per enrollment, enrollments of soft-deleted classes excluded.
        new(ClassEnrollment, "Số lượt ghi danh tạo trong khoảng thời gian (theo EnrolledAt); status là trạng thái hiện tại của lượt ghi danh.",
            "enrollments", "enrollments", AnalyticsSource.Enrollments, [Day, Week, Month, Status, Class],
            [F.From, F.To, F.Status, F.ClassId]),
        new(EnrollmentStatusMix, "Số lượt ghi danh hiện có theo trạng thái (Active / Withdrawn), không theo khoảng ngày.",
            "enrollments", "enrollments", AnalyticsSource.Enrollments, [Status, Class], [F.ClassId],
            RequiredDimension: Status),
        new(EnrolledLearners, "Số học viên khác nhau có lượt ghi danh tạo trong khoảng thời gian (theo EnrolledAt).",
            "learners", "learners", AnalyticsSource.Enrollments, [Day, Week, Month], [F.From, F.To, F.Status],
            Suppressed: true),

        // Class cohorts: classes whose StartDate falls in the range.
        new(WithdrawRate, "Tỉ lệ lượt ghi danh đã rút (Withdrawn) trên tổng lượt ghi danh của các lớp bắt đầu (StartDate) trong khoảng.",
            "ratio", "enrollments", AnalyticsSource.ClassCohorts, [Month, Class], [F.From, F.To, F.ClassId],
            Suppressed: true),
        new(ClassSizeAvg, "Số học viên đang học (ghi danh Active) trung bình mỗi lớp, trên các lớp bắt đầu (StartDate) trong khoảng; lớp chưa có ai tính là 0.",
            "learners/class", "classes", AnalyticsSource.ClassCohorts, [Month], [F.From, F.To],
            Suppressed: true),

        // Teachers: current assignments, never labelled.
        new(TeacherLoad, "Số lớp đang mở (Draft + Active) mỗi giáo viên đang được phân công, nhiều nhất trước. Giáo viên chỉ hiện bằng mã băm 8 ký tự hex.",
            "classes", "teachers", AnalyticsSource.Teachers, [Teacher], [],
            RequiredDimension: Teacher, Sort: AnalyticsSort.ValueDescending)
    ];

    /// <summary>Metrics that need data of another service; listed so callers know they are not available yet.</summary>
    public static readonly IReadOnlyList<PlannedAnalyticsMetric> Planned =
    [
        new("class_avg_score", "classroom",
            "Điểm trung bình theo lớp: cần ghép ClassEnrollment với lượt làm bài của Assessment (khác database).", V.Teacher)
    ];

    private static readonly Dictionary<string, AnalyticsMetric> ByName =
        Metrics.ToDictionary(metric => metric.Name, StringComparer.Ordinal);

    public static AnalyticsMetric? Find(string name) => ByName.GetValueOrDefault(name);

    public static bool IsPlanned(string name) => Planned.Any(metric => metric.Name == name);
}
