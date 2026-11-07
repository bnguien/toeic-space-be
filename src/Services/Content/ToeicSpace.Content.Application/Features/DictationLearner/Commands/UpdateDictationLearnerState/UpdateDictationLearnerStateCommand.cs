using System.Text.Json.Serialization;
using ToeicSpace.Content.Application.DTOs.Dictation;

namespace ToeicSpace.Content.Application.Features.DictationLearner.Commands.UpdateDictationLearnerState;

/// <summary>
/// Body of <c>PUT me/state</c>, every section optional: stars and seconds per day merge by the larger value,
/// settings / voice / sound replace the stored value only when their timestamp (Unix ms) is newer.
/// </summary>
public sealed record UpdateDictationLearnerStateCommand(
    Dictionary<string, int>? Stars = null,
    Dictionary<string, int>? DailySeconds = null,
    DictationSettingsPairDto? Settings = null,
    long? SettingsUpdatedAt = null,
    string? Voice = null,
    long? VoiceUpdatedAt = null,
    SoundPreferencesDto? Sound = null,
    long? SoundUpdatedAt = null,
    [property: JsonIgnore] Guid LearnerId = default) : IRequest<DictationLearnerStateDto>;
