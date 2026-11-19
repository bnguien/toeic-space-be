using ToeicSpace.Content.Application.DTOs.Speech;
using ToeicSpace.Content.Application.Interfaces.Services;
using ToeicSpace.Content.Domain.Rules;

namespace ToeicSpace.Content.Application.Features.Speech.Queries.GetSpeechAudio;

public sealed class GetSpeechAudioHandler(ISpeechSynthesizer synthesizer)
    : IRequestHandler<GetSpeechAudioQuery, SpeechClipDto>
{
    public async Task<SpeechClipDto> Handle(GetSpeechAudioQuery request, CancellationToken ct)
    {
        var voice = SpeechVoices.Resolve(request.Voice);
        var clip = await synthesizer.SynthesizeAsync(SpeechVoices.CleanText(request.Text), voice,
            SpeechVoices.ClampRate(request.Rate), ct);
        return clip with { FileName = $"speech-{voice.Key}.mp3" };
    }
}
