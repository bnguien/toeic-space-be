using ToeicSpace.Content.Domain.Enums;

namespace ToeicSpace.Content.Domain.Rules.Videos;

/// <summary>Outcome of checking whether a video plays in the embedded player, with a short reason.</summary>
public sealed record EmbedCheck(VideoEmbedStatus Status, string Reason)
{
    public static readonly EmbedCheck Playable = new(VideoEmbedStatus.Playable, string.Empty);
    public static EmbedCheck Unknown(string reason) => new(VideoEmbedStatus.Unknown, reason);
    public static EmbedCheck Blocked(string reason) => new(VideoEmbedStatus.Blocked, reason);
}

/// <summary>
/// Studychill <c>video_embed.py</c>: browser reports are only a signal; the server re-checks. oEmbed 401 = embedding
/// disabled, 403 = private, 400/404 = removed; oEmbed 200 is also returned for age-restricted and members-only
/// videos, so the watch page's playability status decides. Inconclusive (bot check, temporary UNPLAYABLE) → never hide.
/// </summary>
public static class EmbedPlayabilityRules
{
    /// <summary>IFrame error codes worth re-checking: 100 not found/private, 101 and 150 embedding not allowed.</summary>
    public static readonly IReadOnlySet<int> UnplayableCodes = new HashSet<int> { 100, 101, 150 };

    public static EmbedCheck? FromOEmbedStatus(int statusCode) => statusCode switch
    {
        401 => EmbedCheck.Blocked("no_embed"),
        403 => EmbedCheck.Blocked("private"),
        400 or 404 => EmbedCheck.Blocked("removed"),
        200 => null,
        _ => EmbedCheck.Unknown($"oembed_{statusCode}")
    };

    /// <summary>Classifies <c>ytInitialPlayerResponse.playabilityStatus</c>.</summary>
    public static EmbedCheck Classify(string? status, string? reason, bool? playableInEmbed)
    {
        var why = (reason ?? string.Empty).ToLowerInvariant();
        switch (status ?? string.Empty)
        {
            case "OK":
                return playableInEmbed == false ? EmbedCheck.Blocked("no_embed") : EmbedCheck.Playable;
            case "LOGIN_REQUIRED":
                if (why.Contains("bot")) return EmbedCheck.Unknown("bot_check");
                if (why.Contains("age")) return EmbedCheck.Blocked("age");
                if (why.Contains("private")) return EmbedCheck.Blocked("private");
                return EmbedCheck.Unknown("login");
            case "ERROR":
                return EmbedCheck.Blocked("removed");
            case "UNPLAYABLE":
                if (why.Contains("member")) return EmbedCheck.Blocked("members");
                if (why.Contains("country") || why.Contains("region")) return EmbedCheck.Blocked("geo");
                // YouTube answers UNPLAYABLE temporarily when called in bursts: not enough evidence to hide.
                return EmbedCheck.Unknown("unplayable");
            default:
                var lowered = (status ?? string.Empty).ToLowerInvariant();
                return EmbedCheck.Unknown(lowered.Length > 16 ? lowered[..16] : lowered);
        }
    }
}
