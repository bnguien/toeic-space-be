namespace ToeicSpace.Content.Application.DTOs.News;

public sealed record DictationWordDto(string Text, bool Ok);

/// <summary>
/// Grade of one dictation sentence. <paramref name="Recorded"/> = saved to the signed-in learner's reading
/// (guests are graded but nothing is stored).
/// </summary>
public sealed record DictationResultDto(bool Correct, IReadOnlyList<DictationWordDto> Words, int CorrectCount,
    int Total, int Extra, bool Recorded);

/// <summary>Dictation progress of one sentence; <c>Result</c> is the grade of the last <c>Input</c>.</summary>
public sealed record NewsDictationEntryDto(bool Passed, bool Unaided, bool Revealed, int Attempts, string? Input,
    DictationResultDto? Result);

/// <summary>Learner-wide reader settings; every value optional.</summary>
public sealed record NewsReaderSettingsDto(string? Palette, string? Mode, string? Layout, int? FontSize, bool? Dark,
    int? PanelWidth, int? Split, bool? ShowSuggested, bool? ShowLookedUp, bool? ShowMeaning, string? HackMode,
    string? Voice);

/// <summary>
/// The signed-in learner's state of one article. <paramref name="Settings"/> = the learner's most recently written
/// reader settings (from any article), null when never saved.
/// </summary>
public sealed record NewsReadingStateDto(string Slug, bool Done, DateTime OpenedAt, DateTime? CompletedAt,
    int ReadingSeconds, IReadOnlyList<string> KnownWords, string Notes,
    IReadOnlyDictionary<string, NewsDictationEntryDto> Dictation, int DictationTotal, NewsReaderSettingsDto? Settings,
    DateTime? SettingsUpdatedAt, DateTime UpdatedAt);

/// <summary>Dictation progress of one sentence sent by the client (merge: flags only move forward).</summary>
public sealed record NewsDictationEntryInput(bool Passed, bool Unaided, bool Revealed, int Attempts, string? Input);

/// <summary>Result of merging a guest's local reading state into the account.</summary>
public sealed record NewsMergeResultDto(int Merged, int Skipped);

/// <summary>Body of <c>POST articles/{slug}/dictation</c>.</summary>
public sealed record CheckDictationRequest(string SentenceId, string Text, bool Revealed = false);

/// <summary>Body of <c>PUT articles/{slug}/state</c> (every property optional, see UpdateNewsReadingStateCommand).</summary>
public sealed record UpdateReadingStateRequest(int? ReadingSeconds = null, IReadOnlyList<string>? KnownWords = null,
    string? Notes = null, IReadOnlyDictionary<string, NewsDictationEntryInput>? Dictation = null,
    bool ResetDictation = false, NewsReaderSettingsDto? Settings = null);
