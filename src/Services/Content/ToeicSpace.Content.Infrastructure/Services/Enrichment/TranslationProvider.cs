using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ToeicSpace.Content.Application.Interfaces.Services.Enrichment;
using ToeicSpace.Content.Application.Models.Enrichment;
using ToeicSpace.Content.Application.Services.Enrichment;

namespace ToeicSpace.Content.Infrastructure.Services.Enrichment;

/// <summary>
/// Tier 4: English → Vietnamese. Google Cloud Translation v2 (only with <c>GoogleTranslate:ApiKey</c>) → Google gtx
/// free endpoint → MyMemory. 4 s per provider; empty, echoed or warning results are rejected and the next provider is
/// tried. Request logging is disabled for this client because the paid API key is in the query string.
/// </summary>
public sealed class TranslationProvider(HttpClient http, IOptions<GoogleTranslateOptions> options,
    ILogger<TranslationProvider> logger) : ITranslationProvider
{
    public static readonly TimeSpan Timeout = TimeSpan.FromSeconds(4);
    public const string BrowserUserAgent =
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/124.0 Safari/537.36";

    public async Task<TranslationResult?> TranslateAsync(string text, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        var input = text.Trim();
        if (!string.IsNullOrWhiteSpace(options.Value.ApiKey)
            && Accept(input, await TryAsync("google-cloud", t => GoogleCloudAsync(input, t), ct)) is { } cloud)
            return new TranslationResult(cloud, GlossSource.Google);
        if (Accept(input, await TryAsync("gtx", t => GtxAsync(input, t), ct)) is { } gtx)
            return new TranslationResult(gtx, GlossSource.Google);
        if (Accept(input, await TryAsync("mymemory", t => MyMemoryAsync(input, t), ct)) is { } memory)
            return new TranslationResult(memory, GlossSource.MyMemory);
        return null;
    }

    private static string? Accept(string input, string? output) =>
        TranslationFilter.IsJunk(input, output) ? null : WebUtility.HtmlDecode(output!.Trim());

    private async Task<string?> TryAsync(string provider, Func<CancellationToken, Task<string?>> call, CancellationToken ct)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(Timeout);
        try { return await call(timeout.Token); }
        catch (Exception ex) when (ex is HttpRequestException or JsonException or InvalidOperationException
                                       || ex is OperationCanceledException && !ct.IsCancellationRequested)
        {
            logger.LogDebug("Translation provider {Provider} failed: {Error}", provider, ex.GetType().Name);
            return null;
        }
    }

    private async Task<string?> GoogleCloudAsync(string text, CancellationToken ct)
    {
        var url = "https://translation.googleapis.com/language/translate/v2?key=" + Uri.EscapeDataString(options.Value.ApiKey!);
        using var response = await http.PostAsJsonAsync(url, new { q = text, source = "en", target = "vi", format = "text" }, ct);
        response.EnsureSuccessStatusCode();
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
        return document.RootElement.GetProperty("data").GetProperty("translations")[0]
            .GetProperty("translatedText").GetString();
    }

    private async Task<string?> GtxAsync(string text, CancellationToken ct)
    {
        var url = "https://translate.googleapis.com/translate_a/single?client=gtx&sl=en&tl=vi&dt=t&q=" +
                  Uri.EscapeDataString(text);
        using var document = JsonDocument.Parse(await GetAsync(url, ct));
        return ParseGtx(document.RootElement);
    }

    private async Task<string?> MyMemoryAsync(string text, CancellationToken ct)
    {
        var url = "https://api.mymemory.translated.net/get?langpair=en%7Cvi&q=" + Uri.EscapeDataString(text);
        using var document = JsonDocument.Parse(await GetAsync(url, ct));
        var root = document.RootElement;
        if (root.TryGetProperty("responseStatus", out var status) && status.ToString() != "200") return null;
        return root.GetProperty("responseData").GetProperty("translatedText").GetString();
    }

    private async Task<string> GetAsync(string url, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.TryAddWithoutValidation("User-Agent", BrowserUserAgent);
        request.Headers.TryAddWithoutValidation("Accept", "application/json,text/plain,*/*");
        using var response = await http.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync(ct);
    }

    /// <summary>gtx returns [[["dịch","source",…],…],…]: the translation is the concatenation of [0][i][0].</summary>
    public static string? ParseGtx(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Array || root.GetArrayLength() == 0
            || root[0].ValueKind != JsonValueKind.Array) return null;
        var text = new StringBuilder();
        foreach (var segment in root[0].EnumerateArray())
            if (segment.ValueKind == JsonValueKind.Array && segment.GetArrayLength() > 0
                && segment[0].ValueKind == JsonValueKind.String)
                text.Append(segment[0].GetString());
        return text.Length == 0 ? null : text.ToString();
    }
}
