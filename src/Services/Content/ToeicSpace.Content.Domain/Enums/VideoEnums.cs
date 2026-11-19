namespace ToeicSpace.Content.Domain.Enums;

/// <summary>Whether a catalog video has real English subtitles (learned when someone opens it).</summary>
public enum VideoSubtitleStatus { Unknown = 0, Available = 1, Missing = 2 }

/// <summary>Whether a catalog video plays in the embedded (IFrame) player — checked by the server itself.</summary>
public enum VideoEmbedStatus { Unknown = 0, Playable = 1, Blocked = 2 }

/// <summary>Listening path group by CEFR: Easy (A1–A2), Medium (B1–B2), Hard (C1+).</summary>
public enum VideoTrack { None = 0, Easy = 1, Medium = 2, Hard = 3 }

/// <summary>Where a catalog video's level comes from: channel estimate or scored from its subtitles.</summary>
public enum VideoLevelSource { Estimate = 0, Subtitles = 1 }

/// <summary>Origin of a stored subtitle track.</summary>
public enum SubtitleTrackSource { Imported = 1, YouTube = 2, Translated = 3 }
