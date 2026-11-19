namespace ToeicSpace.Content.Domain.Entities;

/// <summary>
/// Everything one learner keeps about one news article (unique per learner and article): open/complete, reading
/// clock, known words, notes, dictation progress per sentence and the reader settings last written while on it.
/// </summary>
public sealed class NewsReading
{
    public const int NotesMaxLength = 4000;
    public const int MaxKnownWords = 40;

    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid LearnerId { get; set; }
    public Guid ArticleId { get; set; }
    public NewsArticle? Article { get; set; }
    /// <summary>First time the learner opened the article; reloading the page keeps it.</summary>
    public DateTime OpenedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    /// <summary>Reading clock of the article (seconds, idle time excluded by the client); never decreases.</summary>
    public int ReadingSeconds { get; set; }
    /// <summary>Prepared vocabulary words the learner marked as known.</summary>
    public List<string> KnownWords { get; set; } = [];
    public string? Notes { get; set; }
    /// <summary>Dictation ("Nghe chép") progress by sentence id.</summary>
    public Dictionary<string, NewsDictationEntry> Dictation { get; set; } = [];
    /// <summary>Learner-wide reader settings as last written while reading this article.</summary>
    public NewsReaderSettings? Settings { get; set; }
    public DateTime? SettingsUpdatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public bool IsDone => CompletedAt is not null;

    public static NewsReading Open(Guid learnerId, Guid articleId, DateTime now) =>
        new() { LearnerId = learnerId, ArticleId = articleId, OpenedAt = now, UpdatedAt = now };

    /// <summary>Marks the article as read; completing again keeps the first completion time.</summary>
    public void Complete(DateTime now)
    {
        if (CompletedAt is not null) return;
        CompletedAt = now;
        UpdatedAt = now;
    }

    /// <summary>The reading clock never goes back (two tabs or devices report their own totals).</summary>
    public void AddReadingTime(int seconds, DateTime now)
    {
        if (seconds <= ReadingSeconds) return;
        ReadingSeconds = seconds;
        UpdatedAt = now;
    }

    public void SetKnownWords(IEnumerable<string> words, DateTime now)
    {
        KnownWords = words.Distinct().Take(MaxKnownWords).ToList();
        UpdatedAt = now;
    }

    public void SetNotes(string? notes, DateTime now)
    {
        Notes = string.IsNullOrEmpty(notes) ? null : notes;
        UpdatedAt = now;
    }

    public void SetSettings(NewsReaderSettings settings, DateTime now)
    {
        Settings = settings;
        SettingsUpdatedAt = now;
        UpdatedAt = now;
    }

    public void ResetDictation(DateTime now)
    {
        Dictation = [];
        UpdatedAt = now;
    }

    /// <summary>One graded attempt of a sentence: passed stays passed, <c>unaided</c> = passed without a reveal.</summary>
    public void RecordDictationAttempt(string sentenceId, string input, bool correct, bool revealed, DateTime now)
    {
        var entry = EntryFor(sentenceId);
        entry.Revealed |= revealed;
        entry.Attempts++;
        entry.Input = input;
        if (correct && !entry.Passed)
        {
            entry.Passed = true;
            entry.Unaided = !entry.Revealed;
        }
        UpdatedAt = now;
    }

    /// <summary>Merges dictation progress kept elsewhere (another tab, a guest session): flags only move forward.</summary>
    public void MergeDictation(string sentenceId, NewsDictationEntry other, DateTime now)
    {
        var entry = EntryFor(sentenceId);
        if (other.Passed && !entry.Passed)
        {
            entry.Passed = true;
            entry.Unaided = other.Unaided;
        }
        entry.Revealed |= other.Revealed;
        if (other.Attempts > entry.Attempts)
        {
            entry.Attempts = other.Attempts;
            entry.Input = other.Input ?? entry.Input;
        }
        UpdatedAt = now;
    }

    private NewsDictationEntry EntryFor(string sentenceId)
    {
        if (!Dictation.TryGetValue(sentenceId, out var entry))
        {
            entry = new NewsDictationEntry();
            Dictation = new Dictionary<string, NewsDictationEntry>(Dictation) { [sentenceId] = entry };
        }
        return entry;
    }
}

/// <summary>Dictation progress of one sentence.</summary>
public sealed class NewsDictationEntry
{
    public bool Passed { get; set; }
    /// <summary>Passed without revealing the answer first.</summary>
    public bool Unaided { get; set; }
    public bool Revealed { get; set; }
    public int Attempts { get; set; }
    /// <summary>Last text the learner submitted (the per-word marks are recomputed from it).</summary>
    public string? Input { get; set; }
}

/// <summary>Learner-wide reader settings (every value optional; the client keeps its defaults for absent ones).</summary>
public sealed class NewsReaderSettings
{
    /// <summary>Key of the last text palette (the next article picks a different one).</summary>
    public string? Palette { get; set; }
    /// <summary>english (English only) | bilingual.</summary>
    public string? Mode { get; set; }
    /// <summary>inline (translation under each sentence) | columns (two columns).</summary>
    public string? Layout { get; set; }
    public int? FontSize { get; set; }
    public bool? Dark { get; set; }
    public int? PanelWidth { get; set; }
    /// <summary>Width of the English column in two-column layout, percent.</summary>
    public int? Split { get; set; }
    /// <summary>Underline prepared vocabulary.</summary>
    public bool? ShowSuggested { get; set; }
    /// <summary>Bold looked-up words.</summary>
    public bool? ShowLookedUp { get; set; }
    /// <summary>Short meaning above looked-up words.</summary>
    public bool? ShowMeaning { get; set; }
    /// <summary>Hack não mode: english | vietnamese | dictation.</summary>
    public string? HackMode { get; set; }
    /// <summary>Browser voice name of the read-aloud player.</summary>
    public string? Voice { get; set; }
}
