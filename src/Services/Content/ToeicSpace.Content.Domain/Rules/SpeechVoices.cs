namespace ToeicSpace.Content.Domain.Rules;

public sealed record SpeechVoice(string Key, string VoiceName, string Name, string Accent, string Gender, string Flag);

/// <summary>
/// Neural voices of the speech service (one reader per key, several accents), the speaking-rate range and the
/// normalisation every speech request goes through (Studychill <c>listening.services.tts</c>).
/// </summary>
public static class SpeechVoices
{
    public const string DefaultKey = "andrew";
    public const int RateMin = -40;
    public const int RateMax = 40;
    /// <summary>Rates snap to steps of 5 % so a sweep over every rate cannot create 81 files per text.</summary>
    public const int RateStep = 5;
    public const int MaxTextLength = 200;

    public static readonly IReadOnlyList<SpeechVoice> All =
    [
        new("andrew", "en-US-AndrewNeural", "Andrew", "US", "male", "🇺🇸"),
        new("brian", "en-US-BrianNeural", "Brian", "US", "male", "🇺🇸"),
        new("guy", "en-US-GuyNeural", "Guy", "US", "male", "🇺🇸"),
        new("eric", "en-US-EricNeural", "Eric", "US", "male", "🇺🇸"),
        new("christopher", "en-US-ChristopherNeural", "Christopher", "US", "male", "🇺🇸"),
        new("emma", "en-US-EmmaNeural", "Emma", "US", "female", "🇺🇸"),
        new("ava", "en-US-AvaNeural", "Ava", "US", "female", "🇺🇸"),
        new("jenny", "en-US-JennyNeural", "Jenny", "US", "female", "🇺🇸"),
        new("aria", "en-US-AriaNeural", "Aria", "US", "female", "🇺🇸"),
        new("ryan", "en-GB-RyanNeural", "Ryan", "UK", "male", "🇬🇧"),
        new("thomas", "en-GB-ThomasNeural", "Thomas", "UK", "male", "🇬🇧"),
        new("sonia", "en-GB-SoniaNeural", "Sonia", "UK", "female", "🇬🇧"),
        new("libby", "en-GB-LibbyNeural", "Libby", "UK", "female", "🇬🇧"),
        new("natasha", "en-AU-NatashaNeural", "Natasha", "AU", "female", "🇦🇺"),
        new("liam", "en-CA-LiamNeural", "Liam", "CA", "male", "🇨🇦"),
        new("connor", "en-IE-ConnorNeural", "Connor", "IE", "male", "🇮🇪"),
    ];

    public static readonly IReadOnlyDictionary<string, string> AccentLabels = new Dictionary<string, string>
    {
        ["US"] = "Mỹ", ["UK"] = "Anh", ["AU"] = "Úc", ["CA"] = "Canada", ["IE"] = "Ireland",
    };

    public static readonly IReadOnlyDictionary<string, string> GenderLabels = new Dictionary<string, string>
    {
        ["male"] = "Nam", ["female"] = "Nữ",
    };

    /// <summary>Known key or the default voice.</summary>
    public static SpeechVoice Resolve(string? key) =>
        All.FirstOrDefault(v => string.Equals(v.Key, key, StringComparison.Ordinal)) ?? All[0];

    /// <summary>Clamps to [−40, 40] and rounds to the nearest multiple of 5 (half up, like Studychill).</summary>
    public static int ClampRate(double? rate)
    {
        var value = rate is { } r && double.IsFinite(r) ? r : 0;
        value = Math.Clamp(value, RateMin, RateMax);
        return (int)(RateStep * Math.Floor(value / RateStep + 0.5));
    }

    /// <summary>edge-tts prosody rate, e.g. <c>+0%</c>, <c>-25%</c>.</summary>
    public static string RateString(int rate) => rate >= 0 ? $"+{rate}%" : $"{rate}%";

    /// <summary>Collapses whitespace; the cache key and the synthesized text use this form.</summary>
    public static string CleanText(string? text) =>
        string.Join(' ', (text ?? string.Empty).Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
}
