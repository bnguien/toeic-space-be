using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using ToeicSpace.Content.Application.Interfaces.Services.Video;
using ToeicSpace.Content.Domain.Enums;
using ToeicSpace.Content.Domain.Rules.Videos;

namespace ToeicSpace.Content.Infrastructure.Services.Video;

/// <summary>
/// Studychill <c>video_embed.embed_probe</c>: oEmbed (401 no embed, 403 private, 400/404 removed), then the watch page's
/// <c>ytInitialPlayerResponse</c> for age/member/geo blocks. Inconclusive answers never hide a video.
/// </summary>
public sealed class VideoEmbedProbe(HttpClient http, ILogger<VideoEmbedProbe> logger) : IVideoEmbedProbe
{
    private const string Marker = "ytInitialPlayerResponse = ";
    private const int MaxPageChars = 4 * 1024 * 1024;

    public async Task<EmbedCheck> ProbeAsync(string videoId, CancellationToken ct)
    {
        HttpStatusCode code;
        try
        {
            using var timeout = Linked(ct, 4);
            using var oembed = await http.GetAsync("https://www.youtube.com/oembed?format=json&url="
                + Uri.EscapeDataString("https://www.youtube.com/watch?v=" + videoId), timeout.Token);
            code = oembed.StatusCode;
        }
        catch (Exception ex) when (ex is HttpRequestException || ex is OperationCanceledException && !ct.IsCancellationRequested)
        {
            logger.LogInformation("oEmbed of {VideoId} failed: {Error}", videoId, ex.GetType().Name);
            return EmbedCheck.Unknown("oembed_error");
        }
        if (EmbedPlayabilityRules.FromOEmbedStatus((int)code) is { } decided) return decided;
        if (code != HttpStatusCode.OK) return EmbedCheck.Unknown($"oembed_{(int)code}");

        try
        {
            using var timeout = Linked(ct, 8);
            using var request = new HttpRequestMessage(HttpMethod.Get,
                $"https://www.youtube.com/watch?v={videoId}&hl=en&gl=US&bpctr=9999999999&has_verified=1");
            request.Headers.TryAddWithoutValidation("Cookie", "CONSENT=YES+");
            using var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token);
            if (!response.IsSuccessStatusCode) return EmbedCheck.Playable;
            var page = await response.Content.ReadAsStringAsync(timeout.Token);
            if (page.Length > MaxPageChars) page = page[..MaxPageChars];
            var start = page.IndexOf(Marker, StringComparison.Ordinal);
            if (start < 0) return EmbedCheck.Playable;
            var reader = new Utf8JsonReader(System.Text.Encoding.UTF8.GetBytes(page[(start + Marker.Length)..]),
                new JsonReaderOptions { AllowTrailingCommas = true });
            using var document = JsonDocument.ParseValue(ref reader);
            var playability = document.RootElement.TryGetProperty("playabilityStatus", out var ps) ? ps : default;
            string? Read(string name) => playability.ValueKind == JsonValueKind.Object
                && playability.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
            bool? embeddable = playability.ValueKind == JsonValueKind.Object
                && playability.TryGetProperty("playableInEmbed", out var pe) && pe.ValueKind is JsonValueKind.True or JsonValueKind.False
                ? pe.GetBoolean() : null;
            var check = EmbedPlayabilityRules.Classify(Read("status"), Read("reason"), embeddable);
            // oEmbed allowed embedding and the page is inconclusive: treat as playable (never hide without evidence).
            return check.Status == VideoEmbedStatus.Unknown ? EmbedCheck.Playable : check;
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException
                                       || ex is OperationCanceledException && !ct.IsCancellationRequested)
        {
            logger.LogInformation("Watch page of {VideoId} failed: {Error}", videoId, ex.GetType().Name);
            return EmbedCheck.Playable;
        }
    }

    private static CancellationTokenSource Linked(CancellationToken ct, int seconds)
    {
        var source = CancellationTokenSource.CreateLinkedTokenSource(ct);
        source.CancelAfter(TimeSpan.FromSeconds(seconds));
        return source;
    }
}
