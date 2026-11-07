using ToeicSpace.Content.Application.DTOs.Speech;

namespace ToeicSpace.Content.Application.Features.Dictation.Queries.GetExerciseAudio;

/// <summary>mp3 of a sentence in a voice (default: the sentence's voice) and rate (default: the sentence's rate).</summary>
public sealed record GetExerciseAudioQuery(int ItemId, string? Voice, double? Rate) : IRequest<SpeechClipDto>;
