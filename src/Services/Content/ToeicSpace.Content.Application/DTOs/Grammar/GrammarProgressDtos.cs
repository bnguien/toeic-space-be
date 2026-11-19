namespace ToeicSpace.Content.Application.DTOs.Grammar;

public sealed record ScoreDto(int Correct, int Total);

/// <summary><c>Correct = null</c>: the unit's quick check was never submitted.</summary>
public sealed record UnitScoreDto(int? Correct, int Total);

/// <summary>A learner's topic row; <c>QuickCheck</c>/<c>MiniTest</c> null = never submitted (not 0 points).</summary>
public sealed record GrammarLessonProgressDto(
    bool TheoryDone,
    bool VocabDone,
    ScoreDto? QuickCheck,
    IReadOnlyDictionary<string, UnitScoreDto> QuickCheckUnits,
    ScoreDto? MiniTest,
    bool MiniTestPassed,
    int? PassMark,
    bool Completed);

public sealed record StageRequirementDto(int Completed, int Required, int Total);

public sealed record GrammarStageProgressDto(
    bool Unlocked,
    int? Best,
    bool Passed,
    int Total,
    int? PassMark,
    StageRequirementDto Requirement);

public sealed record StudyPositionDto(string Topic, string Part, string Anchor);

public sealed record GrammarStatsDto(int CompletedTopics, int ReviewCount, int ProgressPercent);

public sealed record GrammarProgressDto(
    GrammarStatsDto Stats,
    StudyPositionDto? Current,
    IReadOnlyDictionary<string, GrammarLessonProgressDto> Lessons,
    IReadOnlyDictionary<string, GrammarStageProgressDto> Stages);

public sealed record CompletePartResultDto(GrammarLessonProgressDto Lesson);

public sealed record PositionResultDto(StudyPositionDto Current);

public sealed record KnownWordResultDto(string Topic, string Word, bool Known);

public sealed record GrammarPreferencesDto(bool? DarkMode, int? RoadmapWidth, bool RoadmapCollapsed);

public sealed record MistakeQuestionDto(string QuestionId, string Block, int WrongCount, string? Topic, string? Stage);

public sealed record MistakeGroupDto(string UnitCode, string UnitTitle, string Topic, IReadOnlyList<MistakeQuestionDto> Questions);

public sealed record TrapDto(string TrapCode, string Description, string UnitCode, int WrongCount, int QuestionCount);
