using ToeicSpace.Content.Application.DTOs.Speech;
using ToeicSpace.Content.Domain.Rules;

namespace ToeicSpace.Content.Application.Interfaces.Services;

/// <summary>
/// Neural text-to-speech with a disk cache: the first request for (voice, rate, text) synthesizes an mp3, later
/// requests read the file. Throws <c>SPEECH_UNAVAILABLE</c> (503) when the upstream service fails.
/// </summary>
public interface ISpeechSynthesizer
{
    Task<SpeechClipDto> SynthesizeAsync(string text, SpeechVoice voice, int rate, CancellationToken ct);
}

/// <summary>Background queue that renders the next sentences' audio before the learner presses play.</summary>
public interface ISpeechPrewarmQueue
{
    void Enqueue(IEnumerable<ToeicSpace.Content.Application.Models.Speech.SpeechJob> jobs);
}
