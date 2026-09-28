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
}
