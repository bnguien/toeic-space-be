namespace ToeicSpace.Content.Application.DTOs.Speech;

/// <summary>An mp3 in the speech cache, served as <c>audio/mpeg</c>.</summary>
public sealed record SpeechClipDto(string FilePath, string FileName);
