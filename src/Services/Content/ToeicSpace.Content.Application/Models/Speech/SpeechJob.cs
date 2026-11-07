namespace ToeicSpace.Content.Application.Models.Speech;

/// <summary>A text to render ahead of time in a voice (key) and rate (percent).</summary>
public sealed record SpeechJob(string Text, string VoiceKey, int Rate);
