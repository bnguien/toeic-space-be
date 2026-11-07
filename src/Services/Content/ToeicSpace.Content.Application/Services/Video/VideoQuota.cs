using ToeicSpace.Content.Application.Interfaces.Services.Video;
using ToeicSpace.Content.Domain.Exceptions;

namespace ToeicSpace.Content.Application.Services.Video;

/// <summary>
/// At most 12 NEW videos (not in the store, so YouTube must be called) per learner per hour — YouTube answers bursts
/// with 429 for the whole server. Only called when the request really goes to YouTube; reopening a video is free.
/// The hour restarts with each new video, like Studychill's cache entry.
/// </summary>
public sealed class VideoQuota(IVideoCache cache)
{
    public static readonly TimeSpan Window = TimeSpan.FromHours(1);
    private readonly Lock _gate = new();

    public bool TryConsume(Guid learnerId, string videoId)
    {
        var key = $"video:quota:{learnerId:N}";
        lock (_gate)
        {
            var unlocked = cache.TryGet<string[]>(key, out var list) && list is not null ? list : [];
            if (unlocked.Contains(videoId)) return true;
            if (unlocked.Length >= VideoErrors.MaxNewVideosPerHour) return false;
            cache.Set(key, unlocked.Append(videoId).ToArray(), Window);
            return true;
        }
    }
}
