namespace ToeicSpace.Content.Application.DTOs.Dictation;

public sealed record DictationLevelDto(int Level, string Name, string Band, string Cefr);

public sealed record DictationTopicDto(string Key, string Name);

public sealed record DictationVoiceDto(string Key, string Name, string Accent, string AccentLabel, string Gender,
    string GenderLabel, string Flag);

public sealed record DictationRulesDto(int CleanPassLevel, int SupportGatedLevel, int LevelOneListHideStreak,
    double AssistedRateWarning, int SuggestLevelUpStreak);

/// <summary>A pack that has imported content (active courses).</summary>
public sealed record DictationPackSummaryDto(string Pack, int CourseCount, int ItemCount);

/// <summary>
/// Levels, topics, voices, rate range, the unlock rules of the dictation roadmap and the packs that have content
/// (the catalog hides packs without data).
/// </summary>
public sealed record DictationConfigDto(IReadOnlyList<DictationLevelDto> Levels, IReadOnlyList<DictationTopicDto> Topics,
    IReadOnlyList<DictationVoiceDto> Voices, string DefaultVoice, int RateMin, int RateMax, int PassScore,
    double PassThreshold, DictationRulesDto Rules, IReadOnlyList<DictationPackSummaryDto> Packs);
