namespace ToeicSpace.Content.Domain.Entities;

/// <summary>
/// One row per learner for the dictation state Studychill kept in the browser: stars per stage key, study
/// seconds per local day, play settings of both modes, voice and sound preferences. Stars and seconds merge by
/// the larger value (progress never goes back); settings, voice and sound keep the newer client write.
/// </summary>
public sealed class DictationLearnerState
{
    public const int MaxStarKeys = 2000;
    public const int KeepDays = 14;
    public const int MaxSettingsLength = 4000;

    public Guid LearnerId { get; set; }
    public Dictionary<string, int> Stars { get; set; } = [];
    public Dictionary<string, int> DailySeconds { get; set; } = [];
    /// <summary>Play settings JSON object of the basic mode (English keys, opaque to the server).</summary>
    public string? BasicSettings { get; set; }
    public string? ProSettings { get; set; }
    /// <summary>Client write time in Unix milliseconds; 0 = never written.</summary>
    public long SettingsUpdatedAt { get; set; }
    public string Voice { get; set; } = string.Empty;
    public long VoiceUpdatedAt { get; set; }
    public DictationSoundPreferences? Sound { get; set; }
    public long SoundUpdatedAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public int Version { get; set; }

    /// <summary>Keeps the larger star count per key (1–3). Returns whether anything changed.</summary>
    public bool MergeStars(IReadOnlyDictionary<string, int> incoming)
    {
        var changed = false;
        foreach (var (key, value) in incoming)
        {
            var stars = Math.Clamp(value, 0, 3);
            if (stars == 0) continue;
            if (Stars.TryGetValue(key, out var current) && current >= stars) continue;
            if (!Stars.ContainsKey(key) && Stars.Count >= MaxStarKeys) continue;
            Stars = new Dictionary<string, int>(Stars) { [key] = stars };
            changed = true;
        }
        return changed;
    }

    /// <summary>Keeps the larger number of seconds per day and only the last <see cref="KeepDays"/> days.</summary>
    public bool MergeDailySeconds(IReadOnlyDictionary<string, int> incoming)
    {
        var next = new Dictionary<string, int>(DailySeconds);
        foreach (var (day, value) in incoming)
        {
            var seconds = Math.Clamp(value, 0, 86_400);
            if (next.TryGetValue(day, out var current) && current >= seconds) continue;
            next[day] = seconds;
        }
        foreach (var old in next.Keys.Order(StringComparer.Ordinal).SkipLast(KeepDays).ToList()) next.Remove(old);
        if (next.Count == DailySeconds.Count && next.All(x => DailySeconds.TryGetValue(x.Key, out var v) && v == x.Value))
            return false;
        DailySeconds = next;
        return true;
    }

    public bool ApplySettings(string? basic, string? pro, long updatedAt)
    {
        if (updatedAt <= SettingsUpdatedAt) return false;
        BasicSettings = basic;
        ProSettings = pro;
        SettingsUpdatedAt = updatedAt;
        return true;
    }

    public bool ApplyVoice(string voice, long updatedAt)
    {
        if (updatedAt <= VoiceUpdatedAt) return false;
        Voice = voice;
        VoiceUpdatedAt = updatedAt;
        return true;
    }

    public bool ApplySound(DictationSoundPreferences sound, long updatedAt)
    {
        if (updatedAt <= SoundUpdatedAt) return false;
        Sound = sound;
        SoundUpdatedAt = updatedAt;
        return true;
    }
}

/// <summary>Sound effect preferences: master switch, volume (0.1–1), countdown tick style, chime style.</summary>
public sealed record DictationSoundPreferences(bool Enabled, double Volume, string Tick, string Chime);
