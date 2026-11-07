using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ToeicSpace.Content.Infrastructure.Services.Enrichment.Gemini;

/// <summary>
/// Typed client for <c>models/{model}:generateContent</c> with JSON output. Interactive calls get 8 s for one key
/// attempt plus one retry on another key; batch calls 25 s across all keys. Returns the JSON object text or null.
/// </summary>
public sealed class GeminiClient(HttpClient http, GeminiKeyPool pool, IOptions<GeminiOptions> options,
    ILogger<GeminiClient> logger)
{
    public bool IsConfigured => pool.Count > 0;

    public async Task<string?> GenerateJsonAsync(string model, string systemPrompt, string userPrompt, bool interactive,
        CancellationToken ct)
    {
        if (pool.Count == 0) return null;
        var settings = options.Value;
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct);
        deadline.CancelAfter(TimeSpan.FromSeconds(interactive ? settings.InteractiveTimeoutSeconds : settings.BatchTimeoutSeconds));
        var attempts = pool.GetAttemptOrder(model).Take(interactive ? 2 : pool.Count);
        foreach (var index in attempts)
        {
            if (deadline.IsCancellationRequested) break;
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Post, $"{settings.BaseUrl}{Uri.EscapeDataString(model)}:generateContent")
                {
                    Content = JsonContent.Create(Body(model, systemPrompt, userPrompt))
                };
                request.Headers.TryAddWithoutValidation("x-goog-api-key", pool.KeyAt(index));
                using var response = await http.SendAsync(request, deadline.Token);
                var body = await response.Content.ReadAsStringAsync(deadline.Token);
                if (!response.IsSuccessStatusCode)
                {
                    pool.ReportFailure(index, model, GeminiKeyPool.Classify(response.StatusCode, body));
                    logger.LogInformation("Gemini key #{Index} returned {Status} for {Model}", index,
                        (int)response.StatusCode, model);
                    continue;
                }
                pool.ReportSuccess(index, model);
                return ExtractJson(ReadText(body));
            }
            catch (Exception ex) when (ex is HttpRequestException or JsonException
                                           || ex is OperationCanceledException && !ct.IsCancellationRequested)
            {
                pool.ReportFailure(index, model, GeminiFailure.Transient);
                logger.LogInformation("Gemini key #{Index} failed for {Model}: {Error}", index, model, ex.GetType().Name);
            }
        }
        return null;
    }

    private static object Body(string model, string system, string user)
    {
        var generation = new Dictionary<string, object>
        {
            ["temperature"] = 0.1, ["maxOutputTokens"] = 4096, ["responseMimeType"] = "application/json"
        };
        if (model.StartsWith("gemini-2.5", StringComparison.OrdinalIgnoreCase))
            generation["thinkingConfig"] = new { thinkingBudget = 0 };
        return new
        {
            systemInstruction = new { parts = new[] { new { text = system } } },
            contents = new[] { new { role = "user", parts = new[] { new { text = user } } } },
            generationConfig = generation
        };
    }

    /// <summary>Concatenated text of the first candidate.</summary>
    public static string? ReadText(string responseBody)
    {
        using var document = JsonDocument.Parse(responseBody);
        if (!document.RootElement.TryGetProperty("candidates", out var candidates)
            || candidates.ValueKind != JsonValueKind.Array || candidates.GetArrayLength() == 0) return null;
        if (!candidates[0].TryGetProperty("content", out var content)
            || !content.TryGetProperty("parts", out var parts)) return null;
        var text = new StringBuilder();
        foreach (var part in parts.EnumerateArray())
            if (part.TryGetProperty("text", out var value)) text.Append(value.GetString());
        return text.ToString();
    }

    /// <summary>Strips ``` fences and keeps the first "{" … last "}".</summary>
    public static string? ExtractJson(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        var start = text.IndexOf('{');
        var end = text.LastIndexOf('}');
        return start < 0 || end <= start ? null : text[start..(end + 1)];
    }
}
