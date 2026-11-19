namespace ToeicSpace.Content.Domain.Exceptions;

/// <summary>Error codes of the bilingual video feature (ProblemDetails <c>code</c> + extension members).</summary>
public static class VideoErrors
{
    public const int MaxNewVideosPerHour = 12;

    public static ContentRuleException InvalidVideoUrl() =>
        new("invalid_video_url", "No YouTube video id found in this link (youtube.com/watch?v=... or youtu.be/...).");

    public static ContentRuleException QuotaExceeded() =>
        new("video_quota_exceeded",
            $"You opened {MaxNewVideosPerHour} new videos in the last hour. Take a short break and try again.", 429);

    public static ContentRuleException NoEnglish(string videoId) =>
        new("no_english", "This catalog video has no English subtitles and was hidden.", 404)
        {
            Extensions = new Dictionary<string, object?> { ["videoId"] = videoId }
        };

    public static ContentRuleException VideoUnavailable(string videoId) =>
        new("video_unavailable", "This video cannot be watched or has no subtitles.", 404)
        {
            Extensions = new Dictionary<string, object?> { ["videoId"] = videoId }
        };

    public static ContentRuleException LangUnavailable(IReadOnlyList<string> unavailable, string suggested) =>
        new("lang_unavailable", $"This video has no subtitles for: {string.Join(", ", unavailable)}.")
        {
            Extensions = new Dictionary<string, object?> { ["unavailable"] = unavailable, ["suggested"] = suggested }
        };

    public static ContentRuleException UpstreamUnavailable(IReadOnlyList<string> unavailable, string? videoId = null)
    {
        var extensions = new Dictionary<string, object?> { ["unavailable"] = unavailable };
        if (videoId is not null) extensions["videoId"] = videoId;
        return new ContentRuleException("upstream_unavailable",
            "YouTube is temporarily refusing subtitle downloads. Try again in a few minutes.", 503)
        {
            Extensions = extensions
        };
    }

    public static ContentRuleException TopicNotFound() =>
        new("video_topic_not_found", "Video topic not found.", 404);

    public static ContentRuleException WatchedVideoNotFound() =>
        new("watched_video_not_found", "You have not opened this video.", 404);

    public static ContentRuleException CueNotFound() =>
        new("subtitle_cue_not_found", "Subtitle sentence not found.", 404);

    public static ContentRuleException InvalidWord() =>
        new("invalid_word", "Invalid word.");

    public static ContentRuleException InvalidImportFile(string fileName, string reason) =>
        new("invalid_import_file", $"{fileName}: {reason}");
}
