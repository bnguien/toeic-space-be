using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ToeicSpace.Content.Application.DTOs.Speech;
using ToeicSpace.Content.Application.Interfaces.Services;
using ToeicSpace.Content.Domain.Exceptions;
using ToeicSpace.Content.Domain.Rules;

namespace ToeicSpace.Content.Infrastructure.Services.Speech;

/// <summary>
/// Disk-cached speech: the file name is SHA-256(voice | rate | text). The first request synthesizes through
/// <see cref="EdgeTtsClient"/> into a temporary file and moves it in place (readers never see half a file); one
/// synthesis per key at a time, concurrent requests for the same key await it. Files under 1 KB are treated as
/// broken and rendered again. Upstream failures become <c>SPEECH_UNAVAILABLE</c> (503).
/// </summary>
public sealed class CachedSpeechSynthesizer(EdgeTtsClient client, IOptions<SpeechOptions> options,
    IHostEnvironment environment, ILogger<CachedSpeechSynthesizer> logger) : ISpeechSynthesizer
{
    public const int MinAudioBytes = 1024;
    private readonly ConcurrentDictionary<string, Lazy<Task<string>>> _running = new();
    private string? _directory;

    public async Task<SpeechClipDto> SynthesizeAsync(string text, SpeechVoice voice, int rate, CancellationToken ct)
    {
        var clean = SpeechVoices.CleanText(text);
        if (clean.Length == 0) throw new ContentRuleException("validation_error", "text is required.");
        var rateString = SpeechVoices.RateString(rate);
        var key = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes($"{voice.Key}|{rateString}|{clean}")));
        var path = Path.Combine(Directory(), $"{key}.mp3");
        if (IsValid(path)) return new SpeechClipDto(path, $"{key}.mp3");

        var job = _running.GetOrAdd(key, _ => new Lazy<Task<string>>(() => RenderAsync(clean, voice, rateString, path)));
        try
        {
            var wait = TimeSpan.FromSeconds(options.Value.SynthesisTimeoutSeconds + options.Value.WaitForPeerSeconds);
            return new SpeechClipDto(await job.Value.WaitAsync(wait, ct), $"{key}.mp3");
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            logger.LogWarning(ex, "Speech synthesis failed for voice {Voice}", voice.Key);
            throw DictationErrors.SpeechUnavailable(ex);
        }
    }

    private async Task<string> RenderAsync(string text, SpeechVoice voice, string rate, string path)
    {
        var temp = $"{path}.{Guid.NewGuid():N}.part";
        try
        {
            if (IsValid(path)) return path;
            var audio = await client.SynthesizeAsync(text, voice.VoiceName, rate, CancellationToken.None);
            if (audio.Length < MinAudioBytes) throw new InvalidOperationException("The speech service returned no usable audio.");
            await File.WriteAllBytesAsync(temp, audio);
            File.Move(temp, path, overwrite: true);
            return path;
        }
        finally
        {
            try { File.Delete(temp); } catch (IOException) { /* already moved or locked */ }
            _running.TryRemove(Path.GetFileNameWithoutExtension(path), out _);
        }
    }

    private static bool IsValid(string path)
    {
        try { return new FileInfo(path) is { Exists: true, Length: >= MinAudioBytes }; }
        catch (IOException) { return false; }
    }

    private string Directory()
    {
        if (_directory is not null) return _directory;
        var configured = options.Value.CacheDirectory;
        var directory = string.IsNullOrWhiteSpace(configured)
            ? Path.Combine(environment.ContentRootPath, "App_Data", "speech-cache")
            : Path.GetFullPath(configured, environment.ContentRootPath);
        System.IO.Directory.CreateDirectory(directory);
        return _directory = directory;
    }
}
