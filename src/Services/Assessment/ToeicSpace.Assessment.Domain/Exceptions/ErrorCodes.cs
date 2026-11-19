namespace ToeicSpace.Assessment.Domain.Exceptions;

public static class ErrorCodes
{
    public const string ValidationError = "validation_error";
    public const string NotFound = "not_found";
    public const string Unauthorized = "unauthorized";
    public const string Forbidden = "forbidden";
    public const string Conflict = "conflict";
    public const string InternalError = "internal_server_error";

    public const string ConcurrencyConflict = "concurrency_conflict";
    public const string InvalidReference = "invalid_reference";
    public const string DuplicateCode = "duplicate_code";
    public const string DuplicateQuestionNumber = "duplicate_question_number";
    public const string QuestionLocked = "question_locked";
    public const string PassageInUse = "passage_in_use";
    public const string TestNotPublishable = "test_not_publishable";
    public const string PracticeSetNotPublishable = "practice_set_not_publishable";
    public const string AnswerKeyForbidden = "answer_key_forbidden";

    public const string AttemptUnavailable = "attempt_unavailable";
    public const string AttemptNotActive = "attempt_not_active";
    public const string AttemptNotCompleted = "attempt_not_completed";
    public const string AttemptTimeUp = "attempt_time_up";
    public const string AnswerLocked = "answer_locked";
    public const string AnswerRevisionConflict = "answer_revision_conflict";
    public const string MutationConflict = "mutation_conflict";
    public const string CheckNotAllowed = "check_not_allowed";
    public const string NothingToPractice = "nothing_to_practice";
    public const string StaleStartGeneration = "stale_start_generation";
    public const string LegacyAttemptUnsupported = "legacy_attempt_unsupported";
    public const string MediaPreservationFailed = "media_preservation_failed";

    public const string AnalyticsUnavailable = "analytics_unavailable";
    public const string AnalyticsTimeout = "analytics_timeout";
    public const string AnalyticsRangeTooLarge = "analytics_range_too_large";
}
