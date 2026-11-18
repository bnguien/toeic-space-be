using static ToeicSpace.Assessment.Application.Analytics.Common.AnalyticsDimensions;
using F = ToeicSpace.Assessment.Application.Analytics.Common.AnalyticsFilters;
using V = ToeicSpace.Assessment.Application.Analytics.Common.AnalyticsVisibility;

namespace ToeicSpace.Assessment.Application.Analytics.Common;

/// <summary>
/// The whitelist of metrics the analytics endpoint can compute. Every metric is implemented as fixed
/// EF Core LINQ; request values only select entries of this list.
/// </summary>
public static class AnalyticsCatalog
{
    public const string AttemptCount = "attempt_count";
    public const string ActiveLearners = "active_learners";
    public const string AttemptsPerLearner = "attempts_per_learner";
    public const string CompletionRate = "completion_rate";
    public const string AbandonRate = "abandon_rate";
    public const string AvgScore = "avg_score";
    public const string MedianScore = "median_score";
    public const string AvgListeningScore = "avg_listening_score";
    public const string AvgReadingScore = "avg_reading_score";
    public const string ScoreDistribution = "score_distribution";
    public const string AvgDuration = "avg_duration";
    public const string ReturningRate = "returning_rate";
    public const string ScoreGain = "score_gain";
    public const string TestPopularity = "test_popularity";
    public const string NewLearners = "new_learners";
    public const string Retention7d = "retention_7d";
    public const string AnswerCount = "answer_count";
    public const string Accuracy = "accuracy";
    public const string SkipRate = "skip_rate";
    public const string AvgTimePerQuestion = "avg_time_per_question";
    public const string QuestionPValue = "question_p_value";
    public const string MostMissedQuestions = "most_missed_questions";
    public const string PracticeSetAccuracy = "practice_set_accuracy";
    public const string PracticeSetUsage = "practice_set_usage";
    public const string QuestionExposureRate = "question_exposure_rate";
    public const string QuestionCount = "question_count";
    public const string PassageCount = "passage_count";
    public const string TestCount = "test_count";
    public const string PracticeSetCount = "practice_set_count";
    public const string PracticeSetSize = "practice_set_size";
    public const string TestCompleteness = "test_completeness";

    private static readonly string[] TimeDimensions = [Day, Week, Month];
    private static readonly string[] AttemptDimensions = [Day, Week, Month, Test, Mode, Status];
    private static readonly string[] ScoreDimensions = [Day, Week, Month, Test, Mode];
    private static readonly string[] AnswerDimensions = [Day, Week, Month, Test, Mode, Part, Section, Difficulty];

    private static readonly string[] AttemptFilters = [F.From, F.To, F.TestId, F.Mode, F.Status];
    private static readonly string[] ScoreFilters = [F.From, F.To, F.TestId, F.Mode];
    private static readonly string[] AnswerFilters = [F.From, F.To, F.TestId, F.Mode, F.Part, F.Section, F.Difficulty];

    public static readonly IReadOnlyList<AnalyticsMetric> Metrics =
    [
        // Attempts: one row per attempt started in the date range.
        new(AttemptCount, "Số lượt làm bài bắt đầu trong khoảng thời gian.",
            "attempts", "attempts", AnalyticsSource.Attempts, AttemptDimensions, AttemptFilters),
        new(ActiveLearners, "Số học viên khác nhau có ít nhất một lượt làm bài.",
            "learners", "learners", AnalyticsSource.Attempts, AttemptDimensions, AttemptFilters),
        new(AttemptsPerLearner, "Số lượt làm bài trung bình của mỗi học viên.",
            "attempts/learner", "learners", AnalyticsSource.Attempts, AttemptDimensions, AttemptFilters, Personal: true),
        new(CompletionRate, "Tỉ lệ lượt đã nộp bài (Completed) trên tổng lượt bắt đầu.",
            "ratio", "attempts", AnalyticsSource.Attempts, ScoreDimensions, ScoreFilters),
        new(AbandonRate, "Tỉ lệ lượt bị bỏ dở (Abandoned) trên tổng lượt bắt đầu.",
            "ratio", "attempts", AnalyticsSource.Attempts, ScoreDimensions, ScoreFilters),
        new(AvgScore, "Điểm TOEIC tổng trung bình (10–990) của các lượt Completed.",
            "points", "attempts", AnalyticsSource.Attempts, ScoreDimensions, ScoreFilters),
        new(MedianScore, "Trung vị điểm TOEIC tổng của các lượt Completed.",
            "points", "attempts", AnalyticsSource.Attempts, ScoreDimensions, ScoreFilters),
        new(AvgListeningScore, "Điểm Listening trung bình (5–495) của các lượt Completed.",
            "points", "attempts", AnalyticsSource.Attempts, ScoreDimensions, ScoreFilters),
        new(AvgReadingScore, "Điểm Reading trung bình (5–495) của các lượt Completed.",
            "points", "attempts", AnalyticsSource.Attempts, ScoreDimensions, ScoreFilters),
        new(ScoreDistribution, "Số lượt Completed theo khoảng điểm tổng (bước 100 điểm).",
            "attempts", "attempts", AnalyticsSource.Attempts, [ScoreBand, Test, Mode, Month], ScoreFilters,
            RequiredDimension: ScoreBand),
        new(AvgDuration, "Thời gian làm bài trung bình (giây) của các lượt Completed có ghi thời gian.",
            "seconds", "attempts", AnalyticsSource.Attempts, ScoreDimensions, ScoreFilters),
        new(ReturningRate, "Tỉ lệ học viên hoạt động trong ít nhất 2 ngày khác nhau trong nhóm.",
            "ratio", "learners", AnalyticsSource.Attempts, [Week, Month, Test, Mode], ScoreFilters, Personal: true),
        new(ScoreGain, "Mức tăng điểm trung bình: lượt Completed gần nhất trừ lượt Completed đầu tiên của cùng học viên trong khoảng (chỉ học viên có từ 2 lượt).",
            "points", "learners", AnalyticsSource.Attempts, [Test, Mode], ScoreFilters, Personal: true),
        new(TestPopularity, "Số học viên khác nhau đã làm từng đề, nhiều nhất trước.",
            "learners", "learners", AnalyticsSource.Attempts, [Test, Mode], [F.From, F.To, F.Mode],
            RequiredDimension: Test, Sort: AnalyticsSort.ValueDescending),

        // Cohorts: learners grouped by the date of their first attempt ever.
        new(NewLearners, "Số học viên có lượt làm bài đầu tiên (tính từ trước tới nay) rơi vào nhóm thời gian.",
            "learners", "learners", AnalyticsSource.Cohorts, TimeDimensions, [F.From, F.To], Personal: true),
        new(Retention7d, "Tỉ lệ học viên mới quay lại làm bài trong 1–7 ngày sau ngày đầu tiên. Học viên có ngày đầu chưa đủ 7 ngày bị loại khỏi mẫu.",
            "ratio", "learners", AnalyticsSource.Cohorts, TimeDimensions, [F.From, F.To], Personal: true),

        // Answers: graded answers of attempts started in the date range.
        new(AnswerCount, "Số câu trả lời đã chấm.",
            "answers", "answers", AnalyticsSource.Answers, AnswerDimensions, AnswerFilters, V.Teacher),
        new(Accuracy, "Tỉ lệ đúng = câu đúng / câu đã chấm (câu bỏ trống tính là sai).",
            "ratio", "answers", AnalyticsSource.Answers, AnswerDimensions, AnswerFilters, V.Teacher),
        new(SkipRate, "Tỉ lệ câu bỏ trống trên câu đã chấm.",
            "ratio", "answers", AnalyticsSource.Answers, AnswerDimensions, AnswerFilters, V.Teacher),
        new(AvgTimePerQuestion, "Thời gian trung bình mỗi câu (giây), chỉ tính câu có ghi thời gian.",
            "seconds", "answers", AnalyticsSource.Answers, AnswerDimensions, AnswerFilters, V.Teacher),
        new(QuestionPValue, "Độ khó thực nghiệm (p-value = tỉ lệ đúng) của từng câu, câu khó nhất trước.",
            "ratio", "answers", AnalyticsSource.Questions, [Question, Part, Section, Difficulty], AnswerFilters, V.Teacher,
            RequiredDimension: Question, Sort: AnalyticsSort.ValueAscending),
        new(MostMissedQuestions, "Số lần trả lời sai (kể cả bỏ trống) của từng câu, nhiều nhất trước.",
            "answers", "answers", AnalyticsSource.Questions, [Question, Part, Section, Difficulty], AnswerFilters, V.Teacher,
            RequiredDimension: Question, Sort: AnalyticsSort.ValueDescending),
        new(PracticeSetAccuracy, "Tỉ lệ đúng trên các câu thuộc từng bộ luyện tập, bộ khó nhất trước. Một câu nằm trong nhiều bộ được tính cho mỗi bộ.",
            "ratio", "answers", AnalyticsSource.PracticeSets, [PracticeSet], [F.From, F.To, F.Mode, F.Part], V.Teacher,
            RequiredDimension: PracticeSet, Sort: AnalyticsSort.ValueAscending),
        new(PracticeSetUsage, "Số học viên khác nhau đã trả lời câu của từng bộ luyện tập, nhiều nhất trước.",
            "learners", "learners", AnalyticsSource.PracticeSets, [PracticeSet], [F.From, F.To, F.Mode, F.Part], V.Teacher,
            RequiredDimension: PracticeSet, Sort: AnalyticsSort.ValueDescending),
        new(QuestionExposureRate, "Tỉ lệ câu hỏi Active đã được trả lời ít nhất một lần trong khoảng.",
            "ratio", "questions", AnalyticsSource.Exposure, [Part, Section, Difficulty],
            [F.From, F.To, F.Part, F.Section, F.Difficulty], V.Teacher, Suppressed: false),

        // Bank: current content, soft-deleted items excluded. No learner data, so nothing is suppressed.
        new(QuestionCount, "Số câu hỏi trong ngân hàng (test = null là câu chỉ thuộc ngân hàng).",
            "questions", "questions", AnalyticsSource.Bank, [Part, Section, Difficulty, Status, Test],
            [F.Part, F.Section, F.Difficulty, F.Status, F.TestId], V.Public, Suppressed: false),
        new(PassageCount, "Số đoạn / nhóm câu hỏi (Part 3, 4, 6, 7).",
            "passages", "passages", AnalyticsSource.Bank, [Part, Section, Test], [F.Part, F.Section, F.TestId], V.Teacher,
            Suppressed: false),
        new(TestCount, "Số đề thi.",
            "tests", "tests", AnalyticsSource.Bank, [Status], [F.Status], V.Public, Suppressed: false),
        new(PracticeSetCount, "Số bộ luyện tập.",
            "sets", "sets", AnalyticsSource.Bank, [Part, Section, Kind, Status], [F.Part, F.Section, F.Status], V.Public,
            Suppressed: false),
        new(PracticeSetSize, "Số câu hỏi trong từng bộ luyện tập, bộ lớn nhất trước.",
            "questions", "questions", AnalyticsSource.Bank, [PracticeSet], [F.Part, F.Status], V.Teacher, Suppressed: false,
            RequiredDimension: PracticeSet, Sort: AnalyticsSort.ValueDescending),
        new(TestCompleteness, "Mức đầy đủ của từng đề: số câu hiện có / số câu chuẩn của đề, đề thiếu nhiều nhất trước.",
            "ratio", "questions", AnalyticsSource.Bank, [Test], [F.Status], V.Teacher, Suppressed: false,
            RequiredDimension: Test, Sort: AnalyticsSort.ValueAscending)
    ];

    /// <summary>Metrics other services will serve; listed so callers know what is not available yet.</summary>
    public static readonly IReadOnlyList<PlannedAnalyticsMetric> Planned =
    [
        new("course_lesson_progress", "content", "Tiến độ khoá học / bài học.", V.Teacher),
        new("class_avg_score", "classroom", "Điểm trung bình theo lớp (ghép ClassEnrollment với lượt làm bài Assessment).", V.Teacher),
        new("my_score_trend", "assessment", "Xu hướng điểm của chính học viên (chỉ cho token của học viên đó).", V.Self)
    ];

    private static readonly Dictionary<string, AnalyticsMetric> ByName =
        Metrics.ToDictionary(metric => metric.Name, StringComparer.Ordinal);

    public static AnalyticsMetric? Find(string name) => ByName.GetValueOrDefault(name);

    public static bool IsPlanned(string name) => Planned.Any(metric => metric.Name == name);
}
