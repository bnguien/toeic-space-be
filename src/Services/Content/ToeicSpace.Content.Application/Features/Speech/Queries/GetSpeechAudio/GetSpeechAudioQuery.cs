using ToeicSpace.Content.Application.DTOs.Speech;

namespace ToeicSpace.Content.Application.Features.Speech.Queries.GetSpeechAudio;

/// <summary>Shared speech endpoint: any text up to 200 characters in a voice of the catalog.</summary>
public sealed record GetSpeechAudioQuery(string? Text, string? Voice, double? Rate) : IRequest<SpeechClipDto>;
