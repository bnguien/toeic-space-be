namespace ToeicSpace.Content.Application.DTOs.Grammar;

public sealed record QuestionExplanationDto(string Signal, string Rule);

/// <summary>Correctness of one question; correct answers carry the short explanation.</summary>
public sealed record QuestionResultDto(bool Correct, QuestionExplanationDto? Explanation);

/// <summary>
/// Result of a submitted block. <c>AttemptId</c> is opaque: a number for signed-in learners, a signed <c>g.…</c> ticket
/// (6 h) for guests. <c>Saved = false</c> for guests (graded, nothing stored).
/// </summary>
public sealed record AttemptResultDto(
    string AttemptId,
    IReadOnlyDictionary<string, QuestionResultDto> Results,
    int Score,
    int Total,
    int? PassMark,
    int? Best,
    bool Saved,
    GrammarLessonProgressDto? Lesson,
    GrammarStageProgressDto? Stage);

public sealed record AnswerResultDto(bool Correct, QuestionExplanationDto? Explanation, bool Saved);

/// <summary>GET topics/{topic}: the public payload (answers stripped) with its version (ETag).</summary>
public sealed record GrammarTopicPayload(byte[] Json, string Version);
