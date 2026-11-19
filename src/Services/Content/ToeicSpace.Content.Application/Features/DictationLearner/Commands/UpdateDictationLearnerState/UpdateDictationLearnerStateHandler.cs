using System.Text.Json;
using ToeicSpace.Content.Application.DTOs.Dictation;
using ToeicSpace.Content.Application.Interfaces;
using ToeicSpace.Content.Application.Interfaces.Repositories;
using ToeicSpace.Content.Application.Mappings;
using ToeicSpace.Content.Domain.Entities;
using ToeicSpace.Content.Domain.Rules;

namespace ToeicSpace.Content.Application.Features.DictationLearner.Commands.UpdateDictationLearnerState;

public sealed class UpdateDictationLearnerStateHandler(IDictationProgressRepository repository, IUnitOfWork unitOfWork,
    TimeProvider clock) : IRequestHandler<UpdateDictationLearnerStateCommand, DictationLearnerStateDto>
{
    public async Task<DictationLearnerStateDto> Handle(UpdateDictationLearnerStateCommand request, CancellationToken ct)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        var state = await repository.GetStateAsync(request.LearnerId, true, ct);
        var created = state is null;
        state ??= new DictationLearnerState { LearnerId = request.LearnerId, CreatedAt = now };

        var changed = false;
        if (request.Stars is not null) changed |= state.MergeStars(request.Stars);
        if (request.DailySeconds is not null) changed |= state.MergeDailySeconds(request.DailySeconds);
        if (request.Settings is not null && request.SettingsUpdatedAt is { } settingsAt)
            changed |= state.ApplySettings(Raw(request.Settings.Basic), Raw(request.Settings.Pro), settingsAt);
        if (request.Voice is not null && request.VoiceUpdatedAt is { } voiceAt)
            changed |= state.ApplyVoice(request.Voice.Length == 0 ? string.Empty : SpeechVoices.Resolve(request.Voice).Key,
                voiceAt);
        if (request.Sound is { } sound && request.SoundUpdatedAt is { } soundAt)
            changed |= state.ApplySound(new DictationSoundPreferences(sound.Enabled, Math.Clamp(sound.Volume, 0.1, 1),
                sound.Tick.Trim(), sound.Chime.Trim()), soundAt);

        if (changed || created)
        {
            state.UpdatedAt = now;
            state.Version++;
            if (created) repository.AddState(state);
            await unitOfWork.SaveChangesAsync(ct);
        }
        return state.ToDto();
    }

    private static string? Raw(JsonElement? element) =>
        element is { ValueKind: JsonValueKind.Object } value ? value.GetRawText() : null;
}
