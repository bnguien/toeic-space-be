using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace ToeicSpace.Content.Infrastructure.Services.Pixabay;

/// <summary>Typed HttpClient for the Pixabay videos API (https://pixabay.com/api/docs/#api_search_videos).</summary>
public sealed class PixabayVideoClient(HttpClient http)
{
    public async Task<IReadOnlyList<PixabayVideoHit>> SearchAsync(string baseUrl, string apiKey, string query,
        int perPage, CancellationToken ct)
    {
        var url = $"{baseUrl}?key={Uri.EscapeDataString(apiKey)}&q={Uri.EscapeDataString(query)}" +
                  $"&video_type=film&safesearch=true&order=popular&per_page={perPage}";
        using var response = await http.GetAsync(url, ct);
        // The status alone is reported; the request URI contains the key and must never reach the logs.
        if (!response.IsSuccessStatusCode)
            throw new HttpRequestException($"Pixabay returned {(int)response.StatusCode} for query '{query}'.",
                null, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<PixabayVideoResponse>(ct);
        return body?.Hits ?? [];
    }
}

public sealed class PixabayVideoResponse
{
    [JsonPropertyName("hits")] public List<PixabayVideoHit>? Hits { get; set; }
}

public sealed class PixabayVideoHit
{
    [JsonPropertyName("id")] public long Id { get; set; }
    [JsonPropertyName("pageURL")] public string? PageUrl { get; set; }
    [JsonPropertyName("tags")] public string? Tags { get; set; }
    [JsonPropertyName("duration")] public int Duration { get; set; }
    [JsonPropertyName("user")] public string? User { get; set; }
    [JsonPropertyName("videos")] public PixabayVideoRenditions? Videos { get; set; }
}

public sealed class PixabayVideoRenditions
{
    [JsonPropertyName("large")] public PixabayVideoFile? Large { get; set; }
    [JsonPropertyName("medium")] public PixabayVideoFile? Medium { get; set; }
    [JsonPropertyName("small")] public PixabayVideoFile? Small { get; set; }
    [JsonPropertyName("tiny")] public PixabayVideoFile? Tiny { get; set; }
}

public sealed class PixabayVideoFile
{
    [JsonPropertyName("url")] public string? Url { get; set; }
    [JsonPropertyName("width")] public int Width { get; set; }
    [JsonPropertyName("height")] public int Height { get; set; }
    [JsonPropertyName("thumbnail")] public string? Thumbnail { get; set; }
}
