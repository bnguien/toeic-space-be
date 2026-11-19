namespace ToeicSpace.Content.Infrastructure.Services.Speech;

/// <summary>Configuration section <c>Speech</c> (edge-tts port + disk cache).</summary>
public sealed class SpeechOptions
{
    public const string SectionName = "Speech";

    /// <summary>Directory of the mp3 cache; empty = <c>App_Data/speech-cache</c> under the content root.</summary>
    public string CacheDirectory { get; set; } = string.Empty;
    public int ConnectTimeoutSeconds { get; set; } = 5;
    public int ReceiveTimeoutSeconds { get; set; } = 10;
    /// <summary>Whole synthesis of one text (connect + receive).</summary>
    public int SynthesisTimeoutSeconds { get; set; } = 15;
    /// <summary>A same-key request waits this long for a synthesis already running before giving up (503).</summary>
    public int WaitForPeerSeconds { get; set; } = 5;
    /// <summary>Cache-Control max-age of audio responses (7 days).</summary>
    public int ClientCacheSeconds { get; set; } = 604_800;
}
