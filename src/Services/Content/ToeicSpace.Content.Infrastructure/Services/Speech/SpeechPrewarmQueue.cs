using System.Collections.Concurrent;
using System.Threading.Channels;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ToeicSpace.Content.Application.Interfaces.Services;
using ToeicSpace.Content.Application.Models.Speech;
using ToeicSpace.Content.Domain.Rules;

namespace ToeicSpace.Content.Infrastructure.Services.Speech;

/// <summary>
/// In-memory queue of texts to render ahead (the next sentences of the stage being played, in the learner's
/// voice). A (voice, rate, text) already queued in the last 10 minutes is skipped; when the queue is full new jobs
/// are dropped — prewarming is only an optimisation, the audio endpoint renders on demand anyway.
/// </summary>
public sealed class SpeechPrewarmQueue(TimeProvider clock) : ISpeechPrewarmQueue
{
    private static readonly TimeSpan DedupeWindow = TimeSpan.FromMinutes(10);
    private readonly Channel<SpeechJob> _channel = Channel.CreateBounded<SpeechJob>(
        new BoundedChannelOptions(200) { FullMode = BoundedChannelFullMode.DropWrite, SingleReader = true });
    private readonly ConcurrentDictionary<string, DateTimeOffset> _recent = new();

    public ChannelReader<SpeechJob> Reader => _channel.Reader;

    public void Enqueue(IEnumerable<SpeechJob> jobs)
    {
        var now = clock.GetUtcNow();
        if (_recent.Count > 5000)
            foreach (var old in _recent.Where(x => now - x.Value > DedupeWindow).Select(x => x.Key).ToList())
                _recent.TryRemove(old, out _);
        foreach (var job in jobs)
        {
            var key = $"{job.VoiceKey}|{job.Rate}|{SpeechVoices.CleanText(job.Text)}";
            if (_recent.TryGetValue(key, out var at) && now - at < DedupeWindow) continue;
            _recent[key] = now;
            _channel.Writer.TryWrite(job);
        }
    }
}

/// <summary>Renders queued texts one at a time; a failing text never blocks the next ones.</summary>
public sealed class SpeechPrewarmWorker(SpeechPrewarmQueue queue, ISpeechSynthesizer synthesizer,
    ILogger<SpeechPrewarmWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var job in queue.Reader.ReadAllAsync(stoppingToken))
        {
            try
            {
                await synthesizer.SynthesizeAsync(job.Text, SpeechVoices.Resolve(job.VoiceKey), job.Rate, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                logger.LogInformation("Speech prewarm skipped a text ({Voice}): {Reason}", job.VoiceKey, ex.Message);
            }
        }
    }
}
