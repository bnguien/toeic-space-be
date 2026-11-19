namespace ToeicSpace.Content.Domain.Exceptions;

/// <summary>Error codes of the grammar feature (ProblemDetails <c>code</c>).</summary>
public static class GrammarErrors
{
    public static ContentRuleException TopicNotFound() =>
        new("grammar_topic_not_found", "Grammar topic not found.", 404);

    public static ContentRuleException StageNotFound() =>
        new("grammar_stage_not_found", "Grammar stage test not found.", 404);

    public static ContentRuleException AttemptNotFound() =>
        new("grammar_attempt_not_found", "Attempt not found.", 404);

    public static ContentRuleException QuestionNotFound() =>
        new("grammar_question_not_found", "Question not found.", 404);

    public static ContentRuleException StageLocked() =>
        new("stage_locked", "The stage test is locked: complete at least 80% of the stage's topics first.", 403);

    public static ContentRuleException ContentUnavailable() =>
        new("grammar_content_unavailable", "Grammar content is not available yet.", 503);

    public static ContentRuleException Invalid(string message) => new("validation_error", message);

    public static ContentRuleException InvalidImportFile(string fileName, string reason) =>
        new("invalid_import_file", $"{fileName}: {reason}");

    public static ContentRuleException CommentNotFound() =>
        new("grammar_comment_not_found", "Comment not found.", 404);

    public static ContentRuleException CommentForbidden() =>
        new("grammar_comment_forbidden", "You can only delete your own comments.", 403);
}
