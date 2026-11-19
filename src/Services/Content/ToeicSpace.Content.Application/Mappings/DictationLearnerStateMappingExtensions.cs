using System.Text.Json;
using ToeicSpace.Content.Application.DTOs.Dictation;
using ToeicSpace.Content.Domain.Entities;

namespace ToeicSpace.Content.Application.Mappings;

public static class DictationLearnerStateMappingExtensions
{
    /// <summary>Stored state, or the empty defaults of a learner who never saved anything.</summary>
    public static DictationLearnerStateDto ToDto(this DictationLearnerState? state) =>
        state is null
            ? new DictationLearnerStateDto(new Dictionary<string, int>(), new Dictionary<string, int>(),
                new DictationSettingsPairDto(null, null), 0, string.Empty, 0, null, 0)
            : new DictationLearnerStateDto(state.Stars, state.DailySeconds,
                new DictationSettingsPairDto(ParseObject(state.BasicSettings), ParseObject(state.ProSettings)),
                state.SettingsUpdatedAt, state.Voice, state.VoiceUpdatedAt, state.Sound?.ToDto(), state.SoundUpdatedAt);

    private static JsonElement? ParseObject(string? json)
    {
        if (string.IsNullOrEmpty(json)) return null;
        using var document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }
}
