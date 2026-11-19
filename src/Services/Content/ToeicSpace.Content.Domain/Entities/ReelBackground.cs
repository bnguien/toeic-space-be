namespace ToeicSpace.Content.Domain.Entities;

/// <summary>Muted background clip for Reel mode, cached from a stock video provider.</summary>
public sealed class ReelBackground
{
    public const string PixabayProvider = "pixabay";
    /// <summary>A clip missing from this many consecutive refreshes is deactivated.</summary>
    public const int MaxMisses = 7;

    public Guid Id { get; set; } = Guid.NewGuid();
    public string Provider { get; set; } = string.Empty;
    public string ProviderId { get; set; } = string.Empty;
    public string VideoUrl { get; set; } = string.Empty;
    public string PosterUrl { get; set; } = string.Empty;
    public string PageUrl { get; set; } = string.Empty;
    public string Author { get; set; } = string.Empty;
    public int DurationSeconds { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
    public string Tags { get; set; } = string.Empty;
    public DateTime LastSeenAt { get; set; }
    /// <summary>Consecutive refreshes in which the provider no longer returned the clip.</summary>
    public int MissCount { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public void MarkMissed(DateTime now)
    {
        MissCount++;
        if (MissCount >= MaxMisses) IsActive = false;
        UpdatedAt = now;
    }
}
