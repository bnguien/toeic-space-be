namespace ToeicSpace.Content.Domain.Exceptions;

/// <summary>Error codes of the vocabulary feature (ProblemDetails <c>code</c>).</summary>
public static class VocabularyErrors
{
    public static ContentRuleException StageNotFound() =>
        new("vocabulary_stage_not_found", "Vocabulary stage not found.", 404);

    public static ContentRuleException SetNotFound() =>
        new("vocabulary_set_not_found", "Vocabulary set not found.", 404);

    public static ContentRuleException WordNotFound() =>
        new("vocabulary_word_not_found", "Vocabulary word not found.", 404);

    public static ContentRuleException ClientEventConflict() =>
        new("client_event_conflict", "This clientEventId was already used for another word.", 409);

    public static ContentRuleException InvalidImportFile(string fileName, string reason) =>
        new("invalid_import_file", $"{fileName}: {reason}");

    public static ContentRuleException FolderNotFound() =>
        new("vocabulary_folder_not_found", "Folder not found.", 404);

    public static ContentRuleException FolderNameTaken() =>
        new("folder_name_taken", "A folder with this name already exists.", 409);

    public static ContentRuleException SavedWordNotFound() =>
        new("saved_word_not_found", "Saved word not found.", 404);

    public static ContentRuleException WordNotCustom() =>
        new("word_not_custom", "Only custom words can be edited; official words are read-only.");

    public static ContentRuleException SessionNotFound() =>
        new("study_session_not_found", "Study session not found.", 404);

    public static ContentRuleException SessionNotActive() =>
        new("study_session_not_active", "The study session is completed, abandoned or expired.", 409);

    public static ContentRuleException SessionWordMismatch() =>
        new("study_session_word_mismatch", "The answer is not for the current card of the session.", 409);

    public static ContentRuleException SessionEmpty() =>
        new("study_session_empty", "There are no words to study for this source.");

    public static ContentRuleException CustomWordExists() =>
        new("custom_word_exists", "You already have a custom word with this headword and part of speech.", 409);
}
