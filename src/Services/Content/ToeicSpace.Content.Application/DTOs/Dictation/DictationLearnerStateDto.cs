using System.Text.Json;

namespace ToeicSpace.Content.Application.DTOs.Dictation;

public sealed record SoundPreferencesDto(bool Enabled, double Volume, string Tick, string Chime);

/// <summary>Play settings of both modes as JSON objects (null = never saved).</summary>
public sealed record DictationSettingsPairDto(JsonElement? Basic, JsonElement? Pro);

/// <summary>Timestamps are the client's write times in Unix milliseconds (0 = never written).</summary>
public sealed record DictationLearnerStateDto(IReadOnlyDictionary<string, int> Stars,
    IReadOnlyDictionary<string, int> DailySeconds, DictationSettingsPairDto Settings, long SettingsUpdatedAt,
    string Voice, long VoiceUpdatedAt, SoundPreferencesDto? Sound, long SoundUpdatedAt);
