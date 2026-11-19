using ToeicSpace.Content.Domain.Enums;

namespace ToeicSpace.Content.Application.Analytics.Common;

// Rows the analytics repository returns. Learner IDs stay inside the service: they are only used to count
// distinct learners and never reach a response.

public sealed record CourseFact(
    Guid Id,
    CourseStatus Status,
    CourseLevel Level,
    CourseExamType ExamType,
    DateTime CreatedAt);

/// <summary>Lessons of one course and status, counted in the database.</summary>
public sealed record LessonPartial(
    Guid CourseId,
    LessonStatus Status,
    int Lessons);

/// <param name="LastActivityAt">Completion time, else the last change of the session.</param>
public sealed record StudySessionFact(
    Guid LearnerId,
    StudyMode Mode,
    StudySourceKind Source,
    DateTime StartedAt,
    DateTime LastActivityAt);

/// <summary>A UTC day on which a learner did something recorded by the Content service.</summary>
public sealed record LearnerDay(
    Guid LearnerId,
    DateTime Day);

/// <summary>Reviews of one learner on one day (and one word when grouped by stage), counted in the database.</summary>
/// <param name="Graded">Reviews with a grade other than Seen.</param>
/// <param name="Correct">Good or Easy reviews.</param>
/// <param name="Scheduled">Graded reviews of a word that already had an interval (a recall test).</param>
/// <param name="Recalled">Scheduled reviews that were not graded Forget.</param>
public sealed record ReviewPartial(
    Guid LearnerId,
    DateTime Day,
    Guid? WordId,
    int Reviews,
    int Graded,
    int Correct,
    int Scheduled,
    int Recalled);

/// <summary>A review that moved a word to the mastered interval.</summary>
public sealed record MasteredWordFact(
    Guid LearnerId,
    Guid WordId,
    DateTime ReviewedAt);

/// <summary>Membership of a word in a roadmap stage (through a set).</summary>
public sealed record WordStage(
    Guid WordId,
    Guid StageId,
    int StageOrder);

public sealed record SavedWordPartial(
    Guid LearnerId,
    DateTime Day,
    int Words);

/// <summary>Dictation sentences of one learner, last attempted on one day, in one course.</summary>
public sealed record DictationPartial(
    Guid LearnerId,
    DateTime Day,
    int CourseId,
    int Items,
    int Passed);

public sealed record GrammarAttemptFact(
    Guid LearnerId,
    DateTime CreatedAt,
    string? Topic,
    string? Stage);

/// <summary>Answered grammar questions of one learner, last answered on one day, in one main topic.</summary>
public sealed record GrammarAnswerPartial(
    Guid LearnerId,
    DateTime Day,
    string Topic,
    int Answers,
    int Correct);

public sealed record GrammarLessonFact(
    Guid LearnerId,
    string Topic,
    DateTime UpdatedAt,
    bool Completed);

/// <summary>Which timestamp of a video watch places it in the date range.</summary>
public enum VideoWatchMoment
{
    /// <summary>First time the learner opened the video (CreatedAt).</summary>
    FirstOpened,

    /// <summary>Last progress report (ProgressAt); rows without progress are skipped.</summary>
    LastProgress
}

/// <param name="At">CreatedAt or ProgressAt, following the requested <see cref="VideoWatchMoment"/>.</param>
public sealed record VideoWatchFact(
    Guid LearnerId,
    string VideoId,
    DateTime At,
    int Duration,
    int WatchedPercent);

public sealed record NewsReadingFact(
    Guid LearnerId,
    Guid ArticleId,
    DateTime OpenedAt,
    bool Completed);
