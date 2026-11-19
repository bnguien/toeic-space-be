namespace ToeicSpace.Content.Domain.Exceptions;

/// <summary>Error codes of the dictation and speech features (ProblemDetails <c>code</c>).</summary>
public static class DictationErrors
{
    public static ContentRuleException ItemNotFound() =>
        new("dictation_item_not_found", "Dictation sentence not found.", 404);

    public static ContentRuleException CourseNotFound() =>
        new("dictation_course_not_found", "Dictation course not found.", 404);

    public static ContentRuleException InvalidPack() =>
        new("invalid_pack", "Thiếu hoặc sai mã gói.");

    public static ContentRuleException EmptyItem() =>
        new("dictation_item_empty", "Câu này chưa có nội dung.");

    public static ContentRuleException InvalidImportFile(string fileName, string reason) =>
        new("invalid_import_file", $"{fileName}: {reason}");

    /// <summary>The speech upstream failed; clients fall back to the browser's speechSynthesis.</summary>
    public static ContentRuleException SpeechUnavailable(Exception? inner = null) =>
        new("SPEECH_UNAVAILABLE", "Không tạo được audio trên server.", 503, inner);
}
