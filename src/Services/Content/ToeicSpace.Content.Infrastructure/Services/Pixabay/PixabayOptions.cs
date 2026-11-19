namespace ToeicSpace.Content.Infrastructure.Services.Pixabay;

/// <summary>Configuration section <c>Pixabay</c> (env: <c>Pixabay__ApiKey</c>, <c>Pixabay__Enabled</c>).</summary>
public sealed class PixabayOptions
{
    public const string SectionName = "Pixabay";

    public string? ApiKey { get; set; }
    public bool Enabled { get; set; } = true;
    public string BaseUrl { get; set; } = "https://pixabay.com/api/videos/";
    public TimeSpan RefreshInterval { get; set; } = TimeSpan.FromHours(24);
    /// <summary>Calm scenes without people; one request per query per refresh.</summary>
    public string[] Queries { get; set; } =
        ["nature", "ocean", "forest", "mountain", "sky", "rain", "river", "snow", "sunset", "flowers"];
    public int PerPage { get; set; } = 50;
    public int MinDurationSeconds { get; set; } = 12;
    /// <summary>Pause between requests; Pixabay allows 100 requests per 60 seconds.</summary>
    public TimeSpan RequestDelay { get; set; } = TimeSpan.FromSeconds(1);
}
