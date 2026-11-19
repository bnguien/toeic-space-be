namespace ToeicSpace.Content.Application.DTOs.Dictation;

public sealed record DictationCourseInfoDto(int Id, string Code, string Title, string Pack, int Level);

/// <summary>A sentence of a course with the viewer's progress; never its text.</summary>
public sealed record DictationExerciseDto(int Id, int Level, string SubLevel, int Order, string Topic, int WordCount,
    int? MaxPlays, bool Unlocked, bool Passed, bool PassedClean, double BestAccuracy, int BestScore, int AttemptCount);

public sealed record DictationExercisesDto(int Level, string LevelName, string LevelBand, string SubLevel,
    DictationCourseInfoDto? Course, bool Authenticated, IReadOnlyList<DictationExerciseDto> Exercises);
