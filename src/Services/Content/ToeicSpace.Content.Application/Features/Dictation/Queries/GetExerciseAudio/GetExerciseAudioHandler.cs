using ToeicSpace.Content.Application.DTOs.Speech;
using ToeicSpace.Content.Application.Interfaces.Repositories;
using ToeicSpace.Content.Application.Interfaces.Services;
using ToeicSpace.Content.Domain.Exceptions;
using ToeicSpace.Content.Domain.Rules;

namespace ToeicSpace.Content.Application.Features.Dictation.Queries.GetExerciseAudio;

public sealed class GetExerciseAudioHandler(IDictationCatalogRepository catalog, ISpeechSynthesizer synthesizer)
    : IRequestHandler<GetExerciseAudioQuery, SpeechClipDto>
{
    public async Task<SpeechClipDto> Handle(GetExerciseAudioQuery request, CancellationToken ct)
    {
        var item = await catalog.GetItemAsync(request.ItemId, ct) ?? throw DictationErrors.ItemNotFound();
        var voice = SpeechVoices.Resolve(string.IsNullOrWhiteSpace(request.Voice) ? item.VoiceKey : request.Voice);
        var rate = SpeechVoices.ClampRate(request.Rate ?? item.Rate);
        var clip = await synthesizer.SynthesizeAsync(item.Text, voice, rate, ct);
        return clip with { FileName = $"dictation-{item.Id}-{voice.Key}.mp3" };
    }
}
