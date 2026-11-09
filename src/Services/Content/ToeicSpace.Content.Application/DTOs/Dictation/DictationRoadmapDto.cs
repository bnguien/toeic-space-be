using ToeicSpace.Content.Domain.Enums;

namespace ToeicSpace.Content.Application.DTOs.Dictation;

/// <summary>One stage (= one course) of the roadmap with the viewer's progress; no sentence content.</summary>
public sealed record DictationStageDto(int Number, int CourseId, string Title, IReadOnlyList<int> ItemIds, int ItemCount,
    int PassedCount, int PassedCleanCount, int Stars, DictationStageState State, string SubLevel,
    IReadOnlyList<string> Topics, double WordsPerSentence, int VocabCount);

public sealed record DictationRoadmapLevelDto(int Level, string Name, string Band, string Cefr, int ItemCount,
    int PassedCount, int StagesCompleted, int Stars, IReadOnlyList<DictationStageDto> Stages);

public sealed record DictationRoadmapDto(bool Authenticated, int ItemCount, int PassedCount,
    IReadOnlyList<DictationRoadmapLevelDto> Levels);
