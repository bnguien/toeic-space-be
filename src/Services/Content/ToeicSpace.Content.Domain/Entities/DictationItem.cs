using ToeicSpace.Content.Domain.Enums;

namespace ToeicSpace.Content.Domain.Entities;

/// <summary>
/// One sentence to listen to and write down. The text is the answer and never leaves the server before a
/// submission; keywords, grammar notes and pre-learn vocabulary are document data kept in JSON columns.
/// </summary>
public sealed class DictationItem
{
    public int Id { get; set; }
    public int CourseId { get; set; }
    public DictationCourse? Course { get; set; }
    /// <summary>Natural key from the source data, e.g. <c>sc_stmt_t250_001_01</c>.</summary>
    public string SourceCode { get; set; } = string.Empty;
    public int Order { get; set; }
    /// <summary>Roadmap level of the course (1–5), copied for unlock rules.</summary>
    public int Level { get; set; }
    public string Topic { get; set; } = string.Empty;
    public string Text { get; set; } = string.Empty;
    public string Translation { get; set; } = string.Empty;
    /// <summary>Whole-sentence IPA.</summary>
    public string Ipa { get; set; } = string.Empty;
    /// <summary>Default voice key (empty = service default).</summary>
    public string VoiceKey { get; set; } = string.Empty;
    /// <summary>Default speaking rate in percent (edge-tts), −40..40.</summary>
    public int Rate { get; set; }
    public int? MaxPlays { get; set; }
    public List<DictationKeyword> Keywords { get; set; } = [];
    public List<DictationNote> Notes { get; set; } = [];
    public List<DictationVocabEntry> Vocab { get; set; } = [];
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    /// <summary>Changes with the content; part of the word-proof salt.</summary>
    public DateTime UpdatedAt { get; set; }
}

/// <summary>Keyword chip of a sentence (editor focus words, then CEFR ≥ B1 words of the TOEIC list).</summary>
public sealed record DictationKeyword(string Word, string Meaning, string PartOfSpeech, string Ipa, string Cefr);

/// <summary>Grammar note shown in the answer panel.</summary>
public sealed record DictationNote(string Id, string Text, DictationNoteType Type, string Explanation);

/// <summary>A new word of the sentence, pre-learned in basic mode.</summary>
public sealed record DictationVocabEntry(string Word, string Lemma, string Meaning, string PartOfSpeech, string Ipa);
